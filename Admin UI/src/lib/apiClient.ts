export const API_BASE_URL = import.meta.env.PUBLIC_API_BASE_URL || 'http://localhost:8080';

export async function apiClient(endpoint: string, options: RequestInit = {}) {
  const token = typeof window !== 'undefined' ? localStorage.getItem('token') : null;
  
  const headers: Record<string, string> = {
    ...((options.headers as Record<string, string>) || {}),
  };

  if (!(options.body instanceof FormData) && !headers['Content-Type']) {
    headers['Content-Type'] = 'application/json';
  }

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const selectedBranch = typeof window !== 'undefined'
    ? (localStorage.getItem('cinema_admin_selected_branch') || localStorage.getItem('user_branch'))
    : null;
  if (selectedBranch && selectedBranch !== 'all' && !headers['X-Branch-Id']) {
    headers['X-Branch-Id'] = selectedBranch;
  }

  const url = `${API_BASE_URL}${endpoint.startsWith('/') ? endpoint : `/${endpoint}`}`;
  
  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (!response.ok) {
    if (response.status === 401) {
      if (typeof window !== 'undefined') {
        const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
        window.location.href = `/login?returnUrl=${returnUrl}`;
      }
    }
    let errorMessage = 'An error occurred while fetching the data.';
    try {
      const errorData = await response.json();
      if (errorData.title && errorData.errors) {
        const validationErrors = Object.values(errorData.errors).flat().join(', ');
        errorMessage = `${errorData.title}: ${validationErrors}`;
        if (errorData.detail) {
          errorMessage += ` (${errorData.detail})`;
        }
      } else if (errorData.detail) {
        errorMessage = errorData.detail;
      } else if (errorData.title) {
        errorMessage = errorData.title;
      } else {
        errorMessage = errorData.message || errorMessage;
      }
    } catch (e) {
      // ignore
    }
    throw new Error(errorMessage);
  }

  // If status is 204 No Content, return null
  if (response.status === 204) {
    return null;
  }

  return response.json();
}
