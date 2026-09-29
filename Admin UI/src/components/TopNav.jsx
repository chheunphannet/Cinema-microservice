import * as React from "react";
import TopNavigation from "@cloudscape-design/components/top-navigation";
import { useAuth } from "../hooks/useAuth";
import { API_BASE_URL } from "../lib/apiClient";

const formatRole = (role) => {
  if (!role) return "Staff";
  switch (role.toLowerCase()) {
    case "super_admin":
    case "system_admin":
      return "Super Admin";
    case "content_manager":
      return "Content Manager";
    case "branch_manager":
      return "Branch Manager";
    case "finance_manager":
      return "Finance Manager";
    case "supervisor":
      return "Floor Supervisor";
    case "cashier":
      return "Cashier";
    default:
      return role.replace(/_/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
  }
};

export default function TopNav() {
  const { user, displayName, role, branchId } = useAuth();
  const [branchName, setBranchName] = React.useState(() => {
    if (!branchId || role === "super_admin" || role === "system_admin" || role === "content_manager") {
      return "System-wide (All Branches)";
    }
    try {
      const cached = sessionStorage.getItem("cinema_branches_cache");
      if (cached) {
        const list = JSON.parse(cached);
        const match = list.find((b) => (b.branchId || b.id) === branchId);
        if (match) return match.code ? `${match.name} (${match.code})` : match.name;
      }
    } catch (e) {}
    return "Assigned Branch";
  });

  React.useEffect(() => {
    if (!branchId || role === "super_admin" || role === "system_admin" || role === "content_manager") {
      setBranchName("System-wide (All Branches)");
      return;
    }

    let isMounted = true;
    fetch(`${API_BASE_URL}/api/v1/catalog/branches`)
      .then((res) => (res.ok ? res.json() : []))
      .then((data) => {
        if (!isMounted) return;
        const list = Array.isArray(data)
          ? data
          : Array.isArray(data?.value)
          ? data.value
          : Array.isArray(data?.items)
          ? data.items
          : [];
        try {
          sessionStorage.setItem("cinema_branches_cache", JSON.stringify(list));
        } catch (e) {}

        const match = list.find((b) => (b.branchId || b.id) === branchId);
        if (match) {
          setBranchName(match.code ? `${match.name} (${match.code})` : match.name);
        } else {
          setBranchName(branchId);
        }
      })
      .catch((err) => {
        console.warn("Could not fetch branches in TopNav:", err);
      });

    return () => {
      isMounted = false;
    };
  }, [branchId, role]);

  const handleUtilityClick = (event) => {
    if (event.detail.id === "signout") {
      localStorage.removeItem("token");
      localStorage.removeItem("user");
      localStorage.removeItem("user_role");
      localStorage.removeItem("user_branch");
      localStorage.removeItem("user_display_name");
      localStorage.removeItem("cinema_admin_selected_branch");
      sessionStorage.clear();
      window.location.href = "/login";
    }
  };

  const usernameText = user || displayName || "User";
  const userTitle = displayName && displayName !== user ? `${displayName} (${user})` : usernameText;

  return (
    <TopNavigation
      identity={{
        href: "/",
        title: "Cinema POS Admin",
        logo: {
          src: "/logo/new-logo.svg",
          alt: "Cinema POS Logo",
        },
      }}
      utilities={[
        {
          type: "menu-dropdown",
          ariaLabel: `${usernameText} - ${branchName}`,
          title: userTitle,
          description: branchName,
          disableTextCollapse: true,
          disableUtilityCollapse: true,
          text: (
            <span
              style={{
                display: "inline-flex",
                flexDirection: "column",
                alignItems: "flex-start",
                lineHeight: "1.25",
                textAlign: "left",
                paddingTop: "2px",
                paddingBottom: "2px",
              }}
            >
              <span style={{ fontWeight: 600, fontSize: "13px" }}>{userTitle}</span>
              <span
                style={{
                  fontSize: "11px",
                  opacity: 0.8,
                  fontWeight: 400,
                  maxWidth: "200px",
                  overflow: "hidden",
                  textOverflow: "ellipsis",
                  whiteSpace: "nowrap",
                }}
              >
                {branchName}
              </span>
            </span>
          ),
          iconName: "user-profile",
          onItemClick: handleUtilityClick,
          items: [
            { id: "user-role", text: `Role: ${formatRole(role)}`, disabled: true },
            { id: "user-branch", text: `Branch: ${branchName}`, disabled: true },
            { id: "signout", text: "Sign out" },
          ],
        },
      ]}
      i18nStrings={{
        searchIconAriaLabel: "Search",
        searchDismissIconAriaLabel: "Close search",
        overflowMenuTriggerText: "More",
        overflowMenuTitleText: "All",
        overflowMenuBackIconAriaLabel: "Back",
        overflowMenuDismissIconAriaLabel: "Close menu",
      }}
    />
  );
}

