import { useState } from 'react';

export function useAuth() {
  const [authState] = useState(() => {
    try {
      if (typeof window !== 'undefined') {
        const token = localStorage.getItem('token');
        if (token) {
          const parts = token.split('.');
          if (parts.length === 3) {
            // Fix base64url encoding
            const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
            const payloadStr = atob(base64);
            const payload = JSON.parse(payloadStr);
            
                        let permissions = payload.permission || [];
            if (typeof permissions === 'string') {
              permissions = [permissions];
            }
            
            const rawRole = payload.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || null;
            const roles = Array.isArray(rawRole) ? rawRole : (rawRole ? [rawRole] : []);
            const role = roles.length > 0 ? String(roles[0]).toLowerCase() : null;

            const fallbackBranch = localStorage.getItem('user_branch');
            const branchId = (payload.branch_id && payload.branch_id !== "") ? payload.branch_id : (payload.branchId || fallbackBranch || null);
            const displayName = payload.display_name || payload.name || payload.unique_name || 'User';

            return {
              user: payload.unique_name || payload.name || payload.sub || 'User',
              displayName: displayName,
              role: role,
              roles: roles.map(r => String(r).toLowerCase()),
              branchId: branchId,
              permissions: permissions,
              isAuthenticated: true,
              hasPermission: (perm) => permissions.includes('*.*') || permissions.includes(perm),
              hasRole: (r) => roles.some(x => String(x).toLowerCase() === String(r).toLowerCase())
            };
          }
        }
      }
    } catch (e) {
      console.error('Failed to decode token', e);
    }
    return {
      user: null,
      role: null,
      roles: [],
      branchId: null,
      permissions: [],
      isAuthenticated: false,
      hasPermission: () => false,
      hasRole: () => false
    };
  });

  return authState;
}

