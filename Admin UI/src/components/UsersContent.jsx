import React, { useState } from "react";
import Table from "@cloudscape-design/components/table";
import Box from "@cloudscape-design/components/box";
import SpaceBetween from "@cloudscape-design/components/space-between";
import Button from "@cloudscape-design/components/button";
import TextFilter from "@cloudscape-design/components/text-filter";
import Header from "@cloudscape-design/components/header";
import Pagination from "@cloudscape-design/components/pagination";
import Badge from "@cloudscape-design/components/badge";
import Modal from "@cloudscape-design/components/modal";
import Form from "@cloudscape-design/components/form";
import FormField from "@cloudscape-design/components/form-field";
import Input from "@cloudscape-design/components/input";
import Select from "@cloudscape-design/components/select";
import Alert from "@cloudscape-design/components/alert";
import Flashbar from "@cloudscape-design/components/flashbar";
import Container from "@cloudscape-design/components/container";
import Toggle from "@cloudscape-design/components/toggle";
import StatusIndicator from "@cloudscape-design/components/status-indicator";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../lib/apiClient";
import { QueryWrapper } from "./QueryWrapper.jsx";

export default function UsersContent() {
  return (
    <QueryWrapper>
      <UsersContentInner />
    </QueryWrapper>
  );
}

const ROLE_OPTIONS = [
  { label: "Staff Member", value: "staff" },
  { label: "Cashier", value: "cashier" },
  { label: "Floor Supervisor", value: "supervisor" },
  { label: "Branch Manager", value: "branch_manager" },
  { label: "Finance Manager", value: "finance_manager" },
  { label: "Content Manager", value: "content_manager" },
  { label: "Super Admin", value: "super_admin" },
];

const getRoleBadgeColor = (role) => {
  switch (role?.toLowerCase()) {
    case "super_admin":
    case "system_admin":
      return "red";
    case "branch_manager":
      return "blue";
    case "finance_manager":
      return "green";
    case "content_manager":
      return "purple";
    case "supervisor":
      return "teal";
    default:
      return "grey";
  }
};

const formatRoleLabel = (role) => {
  const match = ROLE_OPTIONS.find((r) => r.value === role?.toLowerCase());
  if (match) return match.label;
  return role ? role.replace(/_/g, " ").replace(/\b\w/g, (c) => c.toUpperCase()) : "Staff";
};

function UsersContentInner() {
  const queryClient = useQueryClient();
  const [filteringText, setFilteringText] = useState("");
  const [currentPageIndex, setCurrentPageIndex] = useState(1);
  const pageSize = 15;

  const [notifications, setNotifications] = useState([]);

  // Modals state
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isPasswordModalOpen, setIsPasswordModalOpen] = useState(false);

  // Form state for Add User modal
  const [formName, setFormName] = useState("");
  const [formUsername, setFormUsername] = useState("");
  const [formEmail, setFormEmail] = useState("");
  const [formPhone, setFormPhone] = useState("");
  const [formPassword, setFormPassword] = useState("");
  const [formRole, setFormRole] = useState(ROLE_OPTIONS[0]);
  const [formBranch, setFormBranch] = useState(null);
  const [formError, setFormError] = useState("");

  // Form state for Edit User modal
  const [editingUser, setEditingUser] = useState(null);
  const [editName, setEditName] = useState("");
  const [editRole, setEditRole] = useState(ROLE_OPTIONS[0]);
  const [editBranch, setEditBranch] = useState(null);
  const [editEmail, setEditEmail] = useState("");
  const [editPhone, setEditPhone] = useState("");
  const [editIsActive, setEditIsActive] = useState(true);
  const [editError, setEditError] = useState("");

  // Form state for Password Reset modal
  const [passwordUser, setPasswordUser] = useState(null);
  const [newPassword, setNewPassword] = useState("");
  const [passwordError, setPasswordError] = useState("");

  const { data: staffData, isLoading, refetch } = useQuery({
    queryKey: ["adminStaff"],
    queryFn: () => apiClient("/api/v1/admin/staff").catch(() => []),
  });

  const { data: branchesData } = useQuery({
    queryKey: ["adminBranchesForUsers"],
    queryFn: () => apiClient("/api/v1/catalog/branches").catch(() => []),
  });

  const ALL_BRANCHES_OPTION = React.useMemo(() => ({ value: "all", label: "All Branches" }), []);
  const NONE_BRANCH = React.useMemo(() => ({ value: "", label: "None (System-wide)" }), []);

  // Map of branchId -> { name, code, label }
  const branchMap = React.useMemo(() => {
    const map = {};
    if (!branchesData) return map;
    const list = Array.isArray(branchesData)
      ? branchesData
      : Array.isArray(branchesData?.value)
      ? branchesData.value
      : Array.isArray(branchesData?.items)
      ? branchesData.items
      : Array.isArray(branchesData?.data)
      ? branchesData.data
      : [];
    list.forEach((b) => {
      const id = b.branchId || b.id;
      if (id) {
        map[id] = {
          id,
          name: b.name || "Unknown Branch",
          code: b.code || "",
          label: b.code ? `${b.name} (${b.code})` : (b.name || "Unknown Branch"),
        };
      }
    });
    return map;
  }, [branchesData]);

  // Options for page-level branch filter
  const filterBranchOptions = React.useMemo(() => {
    if (!branchesData) return [ALL_BRANCHES_OPTION];
    const list = Array.isArray(branchesData)
      ? branchesData
      : Array.isArray(branchesData?.value)
      ? branchesData.value
      : Array.isArray(branchesData?.items)
      ? branchesData.items
      : Array.isArray(branchesData?.data)
      ? branchesData.data
      : [];
    const mapped = list.map((b) => ({
      value: b.branchId || b.id,
      label: b.code ? `${b.name} (${b.code})` : (b.name || "Unknown Branch"),
      description: b.code ? `Branch Code: ${b.code}` : undefined,
    }));
    return [ALL_BRANCHES_OPTION, ...mapped];
  }, [branchesData, ALL_BRANCHES_OPTION]);

  // Options for Add/Edit modals (includes "None (System-wide)")
  const modalBranchOptions = React.useMemo(() => {
    if (!branchesData) return [NONE_BRANCH];
    const list = Array.isArray(branchesData)
      ? branchesData
      : Array.isArray(branchesData?.value)
      ? branchesData.value
      : Array.isArray(branchesData?.items)
      ? branchesData.items
      : Array.isArray(branchesData?.data)
      ? branchesData.data
      : [];
    const mapped = list.map((b) => ({
      value: b.branchId || b.id,
      label: b.code ? `${b.name} (${b.code})` : (b.name || "Unknown Branch"),
      description: b.code ? `Branch Code: ${b.code}` : undefined,
    }));
    return [NONE_BRANCH, ...mapped];
  }, [branchesData, NONE_BRANCH]);

  // Current logged in user context
  const loggedInRole = typeof window !== "undefined" ? (localStorage.getItem("user_role") || "").toLowerCase() : "";
  const loggedInBranch = typeof window !== "undefined" ? (localStorage.getItem("user_branch") || "") : "";
  const isBranchScopedUser = Boolean(
    loggedInBranch &&
    loggedInRole !== "super_admin" &&
    loggedInRole !== "system_admin" &&
    loggedInRole !== "content_manager"
  );

  // Persistent branch selection synced with other admin pages
  const getInitialBranch = () => {
    if (typeof window !== "undefined") {
      if (isBranchScopedUser && loggedInBranch) {
        return { value: loggedInBranch, label: "Loading assigned branch..." };
      }
      const saved =
        localStorage.getItem("cinema_admin_users_branch") ||
        localStorage.getItem("cinema_admin_selected_branch");
      if (saved && saved !== "all") {
        return { value: saved, label: "Loading branch..." };
      }
    }
    return ALL_BRANCHES_OPTION;
  };

  const [selectedBranchOption, setSelectedBranchOption] = useState(getInitialBranch);

  React.useEffect(() => {
    if (typeof window !== "undefined" && filterBranchOptions.length > 1) {
      if (isBranchScopedUser && loggedInBranch) {
        const found = filterBranchOptions.find((b) => b.value === loggedInBranch);
        if (found) {
          setSelectedBranchOption(found);
          return;
        }
      }
      const saved =
        localStorage.getItem("cinema_admin_users_branch") ||
        localStorage.getItem("cinema_admin_selected_branch");
      if (saved && saved !== "all") {
        const found = filterBranchOptions.find((b) => b.value === saved);
        if (found) {
          setSelectedBranchOption(found);
        }
      }
    }
  }, [filterBranchOptions, isBranchScopedUser, loggedInBranch]);

  const handleBranchChange = React.useCallback(
    (option) => {
      if (isBranchScopedUser) return; // Prevent branch-scoped users from switching
      setSelectedBranchOption(option);
      if (typeof window !== "undefined") {
        localStorage.setItem("cinema_admin_users_branch", option.value);
        localStorage.setItem("cinema_admin_selected_branch", option.value);
      }
    },
    [isBranchScopedUser]
  );

  // Normalize staff list
  const allItems = React.useMemo(() => {
    if (!staffData) return [];
    if (Array.isArray(staffData)) return staffData;
    if (Array.isArray(staffData.items)) return staffData.items;
    if (Array.isArray(staffData.data)) return staffData.data;
    return [];
  }, [staffData]);

  // Filter items by branch and text filter
  const filteredItems = React.useMemo(() => {
    return allItems.filter((item) => {
      // Branch filter
      if (selectedBranchOption.value !== "all") {
        if (item.branchId !== selectedBranchOption.value) {
          return false;
        }
      }
      // Text filter
      if (!filteringText) return true;
      const text = filteringText.toLowerCase();
      const name = String(item.displayName ?? item.name ?? item.fullName ?? "").toLowerCase();
      const username = String(item.username ?? "").toLowerCase();
      const email = String(item.email ?? "").toLowerCase();
      const role = String(item.role ?? "").toLowerCase();
      const branchInfo = item.branchId ? branchMap[item.branchId] : null;
      const branchName = String(branchInfo?.name ?? "").toLowerCase();
      const branchCode = String(branchInfo?.code ?? "").toLowerCase();
      return (
        name.includes(text) ||
        username.includes(text) ||
        email.includes(text) ||
        role.includes(text) ||
        branchName.includes(text) ||
        branchCode.includes(text)
      );
    });
  }, [allItems, selectedBranchOption, filteringText, branchMap]);

  // Paginated items
  const paginatedItems = React.useMemo(() => {
    const start = (currentPageIndex - 1) * pageSize;
    return filteredItems.slice(start, start + pageSize);
  }, [filteredItems, currentPageIndex]);

  const pagesCount = Math.max(1, Math.ceil(filteredItems.length / pageSize));

  // --- 1. Create Staff Mutation ---
  const createMutation = useMutation({
    mutationFn: (body) =>
      apiClient("/api/v1/admin/staff", {
        method: "POST",
        body: JSON.stringify(body),
      }),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ["adminStaff"] });
      setIsAddModalOpen(false);
      resetAddForm();
      setNotifications([
        {
          type: "success",
          content: `Staff member '${data.username || "new user"}' created successfully.`,
          dismissible: true,
          id: "create-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
    onError: (err) => {
      setFormError(err.message || "Failed to create staff member.");
    },
  });

  // --- 2. Update Staff Mutation ---
  const updateMutation = useMutation({
    mutationFn: ({ userId, body }) =>
      apiClient(`/api/v1/admin/staff/${userId}`, {
        method: "PUT",
        body: JSON.stringify(body),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["adminStaff"] });
      setIsEditModalOpen(false);
      setNotifications([
        {
          type: "success",
          content: "Staff member details updated successfully.",
          dismissible: true,
          id: "update-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
    onError: (err) => {
      setEditError(err.message || "Failed to update staff member.");
    },
  });

  // --- 3. Password Reset Mutation ---
  const passwordMutation = useMutation({
    mutationFn: ({ userId, newPasswordOrPin }) =>
      apiClient(`/api/v1/admin/staff/${userId}/password`, {
        method: "POST",
        body: JSON.stringify({ newPasswordOrPin }),
      }),
    onSuccess: () => {
      setIsPasswordModalOpen(false);
      setPasswordUser(null);
      setNewPassword("");
      setPasswordError("");
      setNotifications([
        {
          type: "success",
          content: "Staff credentials updated successfully.",
          dismissible: true,
          id: "pwd-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
    onError: (err) => {
      setPasswordError(err.message || "Failed to update password.");
    },
  });

  // --- 4. Quick Toggle Active/Inactive Status ---
  const toggleActiveMutation = useMutation({
    mutationFn: ({ userId, isActive }) =>
      apiClient(`/api/v1/admin/staff/${userId}`, {
        method: "PUT",
        body: JSON.stringify({ isActive }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["adminStaff"] });
      setNotifications([
        {
          type: "success",
          content: "Staff status toggled successfully.",
          dismissible: true,
          id: "toggle-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
    onError: (err) => {
      setNotifications([
        {
          type: "error",
          content: err.message || "Failed to toggle staff status.",
          dismissible: true,
          id: "err-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
  });

  const resetAddForm = () => {
    setFormName("");
    setFormUsername("");
    setFormEmail("");
    setFormPhone("");
    setFormPassword("");
    setFormRole(ROLE_OPTIONS[0]);
    // Default to currently selected branch if valid, not 'all', and role is branch-scoped
    if (selectedBranchOption.value !== "all") {
      const match = modalBranchOptions.find((b) => b.value === selectedBranchOption.value);
      setFormBranch(match || null);
    } else {
      setFormBranch(null);
    }
    setFormError("");
  };

  const handleOpenAddModal = () => {
    resetAddForm();
    setIsAddModalOpen(true);
  };

  const handleCreate = () => {
    if (!formName.trim() || !formUsername.trim() || !formPassword.trim() || !formRole) {
      setFormError("Full Name, Username, Password/PIN, and Role are required.");
      return;
    }
    const isSystemRole = formRole.value === "super_admin" || formRole.value === "content_manager";
    if (!isSystemRole && (!formBranch || !formBranch.value)) {
      setFormError(`Cinema Branch is required for role '${formRole.label}'. Branch-scoped staff must be assigned to a specific branch.`);
      return;
    }
    createMutation.mutate({
      displayName: formName.trim(),
      username: formUsername.trim(),
      email: formEmail.trim() || undefined,
      phone: formPhone.trim() || undefined,
      passwordOrPin: formPassword,
      role: formRole.value,
      branchId: isSystemRole ? undefined : (formBranch?.value ? formBranch.value : undefined),
    });
  };

  const handleOpenEdit = (item) => {
    setEditingUser(item);
    setEditName(item.displayName || item.name || item.fullName || "");
    const roleOpt = ROLE_OPTIONS.find((r) => r.value === item.role?.toLowerCase()) || {
      label: formatRoleLabel(item.role),
      value: item.role,
    };
    setEditRole(roleOpt);
    const isSystemRole = roleOpt.value === "super_admin" || roleOpt.value === "content_manager";
    const branchOpt = isSystemRole ? NONE_BRANCH : (modalBranchOptions.find((b) => b.value === item.branchId) || NONE_BRANCH);
    setEditBranch(branchOpt);
    setEditEmail(item.email || "");
    setEditPhone(item.phone || "");
    setEditIsActive(item.isActive !== false);
    setEditError("");
    setIsEditModalOpen(true);
  };

  const handleSaveEdit = () => {
    if (!editName.trim()) {
      setEditError("Full Name is required.");
      return;
    }
    const isSystemRole = editRole?.value === "super_admin" || editRole?.value === "content_manager";
    if (!isSystemRole && (!editBranch || !editBranch.value)) {
      setEditError(`Cinema Branch is required for role '${editRole?.label || "this role"}'. Branch-scoped staff must be assigned to a specific branch.`);
      return;
    }
    updateMutation.mutate({
      userId: editingUser.userId || editingUser.id,
      body: {
        displayName: editName.trim(),
        role: editRole?.value,
        branchId: isSystemRole ? null : (editBranch?.value ? editBranch.value : null),
        email: editEmail.trim() || null,
        phone: editPhone.trim() || null,
        isActive: editIsActive,
      },
    });
  };

  const handleOpenPassword = (item) => {
    setPasswordUser(item);
    setNewPassword("");
    setPasswordError("");
    setIsPasswordModalOpen(true);
  };

  const handleSavePassword = () => {
    if (!newPassword.trim()) {
      setPasswordError("New Password or PIN cannot be empty.");
      return;
    }
    passwordMutation.mutate({
      userId: passwordUser.userId || passwordUser.id,
      newPasswordOrPin: newPassword.trim(),
    });
  };

  return (
    <Box padding="l">
      {notifications.length > 0 && <Flashbar items={notifications} />}

      <SpaceBetween direction="vertical" size="l">
        {/* Cinema Branch Dropdown Selector Bar */}
        <Container>
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "16px",
              flexWrap: "wrap",
            }}
          >
            <span style={{ fontWeight: 600, fontSize: "14px", color: "#545b64" }}>
              Cinema Branch:
            </span>
            <div style={{ width: "360px", maxWidth: "100%" }}>
              <Select
                disabled={isBranchScopedUser}
                selectedOption={selectedBranchOption}
                onChange={({ detail }) => handleBranchChange(detail.selectedOption)}
                options={filterBranchOptions}
                placeholder="Filter by cinema branch..."
              />
            </div>
            {isBranchScopedUser ? (
              <Badge color="blue">
                Assigned Branch (Locked to your location)
              </Badge>
            ) : (
              selectedBranchOption && (
                <Badge color="blue">
                  {filteredItems.length} {filteredItems.length === 1 ? "Staff Member" : "Staff Members"}
                </Badge>
              )
            )}
            {!isBranchScopedUser && selectedBranchOption?.value !== "all" && (
              <Button variant="link" onClick={() => handleBranchChange(ALL_BRANCHES_OPTION)}>
                Show all branches
              </Button>
            )}
          </div>
        </Container>

        <Table
          columnDefinitions={[
            {
              id: "name",
              header: "Name",
              cell: (item) => (
                <div>
                  <div style={{ fontWeight: 600 }}>
                    {item.displayName ?? item.name ?? item.fullName ?? "-"}
                  </div>
                  {item.phone && (
                    <div style={{ fontSize: "12px", color: "#687078" }}>
                      Tel: {item.phone}
                    </div>
                  )}
                </div>
              ),
            },
            {
              id: "username",
              header: "Username",
              cell: (item) => item.username ?? "-",
            },
            {
              id: "email",
              header: "Email",
              cell: (item) => item.email ?? "-",
            },
            {
              id: "role",
              header: "Role",
              cell: (item) => (
                <Badge color={getRoleBadgeColor(item.role)}>
                  {formatRoleLabel(item.role)}
                </Badge>
              ),
            },
            {
              id: "branch",
              header: "Branch",
              cell: (item) => {
                if (!item.branchId) {
                  return <Badge color="grey">System-wide (All Branches)</Badge>;
                }
                const info = branchMap[item.branchId];
                if (info) {
                  return (
                    <SpaceBetween direction="horizontal" size="xxs">
                      <span>{info.name}</span>
                      {info.code && <Badge color="blue">{info.code}</Badge>}
                    </SpaceBetween>
                  );
                }
                return item.branchName || item.branchId;
              },
            },
            {
              id: "status",
              header: "Status",
              cell: (item) => (
                <StatusIndicator type={item.isActive !== false ? "success" : "stopped"}>
                  {item.isActive !== false ? "Active" : "Inactive"}
                </StatusIndicator>
              ),
            },
            {
              id: "actions",
              header: "Actions",
              cell: (item) => (
                <SpaceBetween direction="horizontal" size="xs">
                  <Button
                    size="small"
                    iconName="edit"
                    onClick={() => handleOpenEdit(item)}
                  >
                    Edit
                  </Button>
                  <Button
                    size="small"
                    iconName="key"
                    onClick={() => handleOpenPassword(item)}
                  >
                    Reset PIN
                  </Button>
                  <Button
                    size="small"
                    variant={item.isActive !== false ? "normal" : "primary"}
                    onClick={() =>
                      toggleActiveMutation.mutate({
                        userId: item.userId || item.id,
                        isActive: !(item.isActive !== false),
                      })
                    }
                    loading={toggleActiveMutation.isPending}
                  >
                    {item.isActive !== false ? "Deactivate" : "Activate"}
                  </Button>
                </SpaceBetween>
              ),
            },
          ]}
          items={paginatedItems}
          loadingText="Loading staff members..."
          loading={isLoading}
          trackBy="userId"
          empty={
            <Box textAlign="center" color="inherit">
              <b>No users found</b>
              <Box padding={{ bottom: "s" }} variant="p" color="inherit">
                No staff members match the current filter or branch selection.
              </Box>
            </Box>
          }
          filter={
            <TextFilter
              filteringPlaceholder="Find users by name, username, email, role, or branch code..."
              filteringText={filteringText}
              onChange={({ detail }) => {
                setFilteringText(detail.filteringText);
                setCurrentPageIndex(1);
              }}
            />
          }
          header={
            <Header
              counter={`(${filteredItems.length})`}
              actions={
                <SpaceBetween direction="horizontal" size="xs">
                  <Button iconName="refresh" onClick={() => refetch()}>
                    Refresh
                  </Button>
                  <Button
                    variant="primary"
                    iconName="add-plus"
                    onClick={handleOpenAddModal}
                  >
                    Add Staff Member
                  </Button>
                </SpaceBetween>
              }
            >
              Staff &amp; Users
            </Header>
          }
          pagination={
            <Pagination
              currentPageIndex={currentPageIndex}
              pagesCount={pagesCount}
              onChange={({ detail }) => setCurrentPageIndex(detail.currentPageIndex)}
            />
          }
        />
      </SpaceBetween>

      {/* -------------------- Add Staff Modal -------------------- */}
      <Modal
        onDismiss={() => setIsAddModalOpen(false)}
        visible={isAddModalOpen}
        header="Add New Staff Member"
        footer={
          <Box float="right">
            <SpaceBetween direction="horizontal" size="xs">
              <Button variant="link" onClick={() => setIsAddModalOpen(false)}>
                Cancel
              </Button>
              <Button
                variant="primary"
                onClick={handleCreate}
                loading={createMutation.isPending}
              >
                Create Staff Member
              </Button>
            </SpaceBetween>
          </Box>
        }
      >
        <Form>
          <SpaceBetween direction="vertical" size="m">
            {formError && (
              <Alert type="error" dismissible onDismiss={() => setFormError("")}>
                {formError}
              </Alert>
            )}
            <FormField label="Full Name" description="Staff member's legal or display name.">
              <Input
                value={formName}
                onChange={({ detail }) => setFormName(detail.value)}
                placeholder="e.g. John Doe"
              />
            </FormField>
            <FormField label="Username" description="Used to log in to POS terminals and Admin Portal.">
              <Input
                value={formUsername}
                onChange={({ detail }) => setFormUsername(detail.value)}
                placeholder="e.g. john.doe"
              />
            </FormField>
            <FormField label="Email Address" description="Optional. For shift notifications and alerts.">
              <Input
                value={formEmail}
                onChange={({ detail }) => setFormEmail(detail.value)}
                placeholder="john.doe@cinema.local"
                type="email"
              />
            </FormField>
            <FormField label="Phone Number" description="Optional. Contact number for shift coordination.">
              <Input
                value={formPhone}
                onChange={({ detail }) => setFormPhone(detail.value)}
                placeholder="+855 12 345 678"
              />
            </FormField>
            <FormField label="Password / PIN" description="Temporary password or numeric PIN for terminal authentication.">
              <Input
                value={formPassword}
                onChange={({ detail }) => setFormPassword(detail.value)}
                type="password"
                placeholder="Enter password or numeric PIN"
              />
            </FormField>
            <FormField label="Role">
              <Select
                selectedOption={formRole}
                onChange={({ detail }) => {
                  setFormRole(detail.selectedOption);
                  if (detail.selectedOption.value === "super_admin" || detail.selectedOption.value === "content_manager") {
                    setFormBranch(NONE_BRANCH);
                  }
                }}
                options={ROLE_OPTIONS}
              />
            </FormField>
            <FormField
              label="Cinema Branch"
              description={
                formRole.value === "super_admin" || formRole.value === "content_manager"
                  ? "System-wide role: Operates globally across all cinema branches. No branch assignment required."
                  : "Required: Select the specific cinema branch where this staff member works."
              }
            >
              <Select
                disabled={formRole.value === "super_admin" || formRole.value === "content_manager"}
                selectedOption={
                  formRole.value === "super_admin" || formRole.value === "content_manager"
                    ? NONE_BRANCH
                    : formBranch
                }
                onChange={({ detail }) => setFormBranch(detail.selectedOption)}
                options={
                  formRole.value === "super_admin" || formRole.value === "content_manager"
                    ? [NONE_BRANCH]
                    : modalBranchOptions.filter((o) => o.value !== "")
                }
                placeholder={
                  formRole.value === "super_admin" || formRole.value === "content_manager"
                    ? "System-wide (All Branches)"
                    : "Select a cinema branch..."
                }
              />
            </FormField>
          </SpaceBetween>
        </Form>
      </Modal>

      {/* -------------------- Edit Staff Modal -------------------- */}
      <Modal
        onDismiss={() => setIsEditModalOpen(false)}
        visible={isEditModalOpen}
        header={`Edit Staff Member: ${editingUser?.username || ""}`}
        footer={
          <Box float="right">
            <SpaceBetween direction="horizontal" size="xs">
              <Button variant="link" onClick={() => setIsEditModalOpen(false)}>
                Cancel
              </Button>
              <Button
                variant="primary"
                onClick={handleSaveEdit}
                loading={updateMutation.isPending}
              >
                Save Changes
              </Button>
            </SpaceBetween>
          </Box>
        }
      >
        <Form>
          <SpaceBetween direction="vertical" size="m">
            {editError && (
              <Alert type="error" dismissible onDismiss={() => setEditError("")}>
                {editError}
              </Alert>
            )}
            <FormField label="Username">
              <Input value={editingUser?.username || ""} disabled />
            </FormField>
            <FormField label="Full Name">
              <Input
                value={editName}
                onChange={({ detail }) => setEditName(detail.value)}
                placeholder="Display Name"
              />
            </FormField>
            <FormField label="Role">
              <Select
                selectedOption={editRole}
                onChange={({ detail }) => {
                  setEditRole(detail.selectedOption);
                  if (detail.selectedOption.value === "super_admin" || detail.selectedOption.value === "content_manager") {
                    setEditBranch(NONE_BRANCH);
                  }
                }}
                options={ROLE_OPTIONS}
              />
            </FormField>
            <FormField
              label="Cinema Branch"
              description={
                editRole.value === "super_admin" || editRole.value === "content_manager"
                  ? "System-wide role: Operates globally across all cinema branches. No branch assignment required."
                  : "Required: Select the specific cinema branch assigned to this staff member."
              }
            >
              <Select
                disabled={editRole.value === "super_admin" || editRole.value === "content_manager"}
                selectedOption={
                  editRole.value === "super_admin" || editRole.value === "content_manager"
                    ? NONE_BRANCH
                    : editBranch
                }
                onChange={({ detail }) => setEditBranch(detail.selectedOption)}
                options={
                  editRole.value === "super_admin" || editRole.value === "content_manager"
                    ? [NONE_BRANCH]
                    : modalBranchOptions.filter((o) => o.value !== "")
                }
                placeholder={
                  editRole.value === "super_admin" || editRole.value === "content_manager"
                    ? "System-wide (All Branches)"
                    : "Select branch..."
                }
              />
            </FormField>
            <FormField label="Email Address">
              <Input
                value={editEmail}
                onChange={({ detail }) => setEditEmail(detail.value)}
                type="email"
                placeholder="Email address"
              />
            </FormField>
            <FormField label="Phone Number">
              <Input
                value={editPhone}
                onChange={({ detail }) => setEditPhone(detail.value)}
                placeholder="Phone number"
              />
            </FormField>
            <FormField label="Account Status">
              <Toggle
                checked={editIsActive}
                onChange={({ detail }) => setEditIsActive(detail.checked)}
              >
                Account is Active
              </Toggle>
            </FormField>
          </SpaceBetween>
        </Form>
      </Modal>

      {/* -------------------- Reset Password Modal -------------------- */}
      <Modal
        onDismiss={() => setIsPasswordModalOpen(false)}
        visible={isPasswordModalOpen}
        header={`Reset Password/PIN: ${passwordUser?.username || ""}`}
        footer={
          <Box float="right">
            <SpaceBetween direction="horizontal" size="xs">
              <Button variant="link" onClick={() => setIsPasswordModalOpen(false)}>
                Cancel
              </Button>
              <Button
                variant="primary"
                onClick={handleSavePassword}
                loading={passwordMutation.isPending}
              >
                Update Credentials
              </Button>
            </SpaceBetween>
          </Box>
        }
      >
        <Form>
          <SpaceBetween direction="vertical" size="m">
            {passwordError && (
              <Alert type="error" dismissible onDismiss={() => setPasswordError("")}>
                {passwordError}
              </Alert>
            )}
            <p>
              Setting a new password or PIN for <b>{passwordUser?.displayName || passwordUser?.username}</b> ({passwordUser?.username}).
            </p>
            <FormField
              label="New Password or Numeric PIN"
              description="Minimum 4 digits for PIN, or 6+ characters for admin password."
            >
              <Input
                value={newPassword}
                onChange={({ detail }) => setNewPassword(detail.value)}
                type="password"
                placeholder="Enter new password or PIN"
              />
            </FormField>
          </SpaceBetween>
        </Form>
      </Modal>
    </Box>
  );
}
