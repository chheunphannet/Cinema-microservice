import * as React from "react";
import ContentLayout from "@cloudscape-design/components/content-layout";
import Header from "@cloudscape-design/components/header";
import Table from "@cloudscape-design/components/table";
import Button from "@cloudscape-design/components/button";
import SpaceBetween from "@cloudscape-design/components/space-between";
import StatusIndicator from "@cloudscape-design/components/status-indicator";
import Alert from "@cloudscape-design/components/alert";
import Modal from "@cloudscape-design/components/modal";
import FormField from "@cloudscape-design/components/form-field";
import Input from "@cloudscape-design/components/input";
import Select from "@cloudscape-design/components/select";
import Flashbar from "@cloudscape-design/components/flashbar";
import Container from "@cloudscape-design/components/container";
import Badge from "@cloudscape-design/components/badge";
import Box from "@cloudscape-design/components/box";
import ColumnLayout from "@cloudscape-design/components/column-layout";
import Toggle from "@cloudscape-design/components/toggle";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useCollection } from "@cloudscape-design/collection-hooks";
import TextFilter from "@cloudscape-design/components/text-filter";
import Pagination from "@cloudscape-design/components/pagination";
import { apiClient } from "../lib/apiClient";
import { QueryWrapper } from "./QueryWrapper";
import { useAuth } from "../hooks/useAuth";

const CATEGORY_OPTIONS = [
  { value: "Combos", label: "Combos" },
  { value: "Popcorn", label: "Popcorn" },
  { value: "Beverages", label: "Beverages" },
  { value: "Snacks", label: "Snacks" },
  { value: "Dessert", label: "Dessert" },
];

const WASTAGE_REASON_OPTIONS = [
  { value: "expired", label: "Expired Stock" },
  { value: "damaged", label: "Damaged in Shipping / Handling" },
  { value: "dropped", label: "Dropped / Spill" },
  { value: "spoilage", label: "Spoilage / Quality Defect" },
  { value: "sampling", label: "Sampling / Promotion" },
  { value: "theft", label: "Theft / Loss Discrepancy" },
  { value: "other", label: "Other" },
];

function FnbThumbnail({ name, category, imageUrl }) {
  const [imgError, setImgError] = React.useState(false);

  React.useEffect(() => {
    setImgError(false);
  }, [imageUrl]);

  const getCategoryTheme = (cat) => {
    const lower = (cat || "").toLowerCase();
    if (lower.includes("popcorn")) {
      return {
        bg: "linear-gradient(135deg, #fef3c7 0%, #fde68a 100%)",
        border: "#f59e0b",
        emoji: "🍿",
      };
    }
    if (lower.includes("bev") || lower.includes("drink") || lower.includes("soda") || lower.includes("water")) {
      return {
        bg: "linear-gradient(135deg, #e0f2fe 0%, #bae6fd 100%)",
        border: "#0284c7",
        emoji: "🥤",
      };
    }
    if (lower.includes("combo")) {
      return {
        bg: "linear-gradient(135deg, #f3e8ff 0%, #e9d5ff 100%)",
        border: "#9333ea",
        emoji: "🍱",
      };
    }
    if (lower.includes("snack") || lower.includes("nacho") || lower.includes("hotdog")) {
      return {
        bg: "linear-gradient(135deg, #fef2f2 0%, #fee2e2 100%)",
        border: "#ef4444",
        emoji: "🥨",
      };
    }
    return {
      bg: "linear-gradient(135deg, #f1f5f9 0%, #e2e8f0 100%)",
      border: "#64748b",
      emoji: "🍿",
    };
  };

  const theme = getCategoryTheme(category);
  const isMockLocal = Boolean(imageUrl && imageUrl.includes(".local"));

  if (!imageUrl || imgError || isMockLocal) {
    return (
      <div
        style={{
          width: "44px",
          height: "44px",
          borderRadius: "8px",
          background: theme.bg,
          border: `1px solid ${theme.border}40`,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: "20px",
          flexShrink: 0,
          boxShadow: "0 1px 2px rgba(0,0,0,0.06)",
        }}
        title={`${name || "Item"} (${category || "F&B"})`}
      >
        <span role="img" aria-label={category || "Item"}>{theme.emoji}</span>
      </div>
    );
  }

  return (
    <img
      src={imageUrl}
      alt={name || "Product image"}
      style={{
        width: "44px",
        height: "44px",
        objectFit: "cover",
        borderRadius: "8px",
        border: "1px solid #cbd5e1",
        backgroundColor: "#f8fafc",
        flexShrink: 0,
        boxShadow: "0 1px 3px rgba(0,0,0,0.08)",
      }}
      onError={() => setImgError(true)}
    />
  );
}

function FnbImageUploadControl({
  imageUrl,
  onChangeImageUrl,
  onFileSelect,
  isUploading,
  uploadError,
  fileInputRef,
  name,
  category,
}) {
  const [imgError, setImgError] = React.useState(false);

  React.useEffect(() => {
    setImgError(false);
  }, [imageUrl]);

  return (
    <FormField
      label="Product Image"
      description="Upload an image to MinIO object storage or enter an image URL directly."
      errorText={uploadError}
    >
      <div style={{ display: "flex", gap: "16px", alignItems: "flex-start", marginTop: "4px" }}>
        <div
          style={{
            width: "72px",
            height: "72px",
            borderRadius: "8px",
            border: "1px solid #cbd5e1",
            backgroundColor: "#f8fafc",
            overflow: "hidden",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            flexShrink: 0,
            position: "relative",
            boxShadow: "0 1px 3px rgba(0,0,0,0.08)",
          }}
        >
          {imageUrl && !imgError && !imageUrl.includes(".local") ? (
            <img
              src={imageUrl}
              alt="Product preview"
              style={{ width: "100%", height: "100%", objectFit: "cover" }}
              onError={() => setImgError(true)}
            />
          ) : (
            <FnbThumbnail name={name} category={category} imageUrl={null} />
          )}
          {isUploading && (
            <div
              style={{
                position: "absolute",
                inset: 0,
                backgroundColor: "rgba(0,0,0,0.65)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                color: "#ffffff",
                fontSize: "10px",
                fontWeight: 600,
              }}
            >
              Uploading...
            </div>
          )}
        </div>

        <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: "8px" }}>
          <input
            type="file"
            ref={fileInputRef}
            onChange={onFileSelect}
            accept="image/png, image/jpeg, image/jpg, image/webp"
            style={{ display: "none" }}
          />
          <SpaceBetween direction="horizontal" size="xs">
            <Button
              iconName="upload"
              onClick={() => fileInputRef.current?.click()}
              loading={isUploading}
            >
              Upload to MinIO
            </Button>
            {imageUrl && (
              <Button
                variant="normal"
                onClick={() => onChangeImageUrl("")}
                disabled={isUploading}
              >
                Clear Image
              </Button>
            )}
          </SpaceBetween>
          <Input
            value={imageUrl}
            onChange={({ detail }) => onChangeImageUrl(detail.value)}
            placeholder="Or enter image URL directly (https://...)"
            disabled={isUploading}
          />
          <div style={{ fontSize: "12px", color: "#64748b" }}>
            Accepts PNG, JPG, JPEG, WEBP (Max 10MB). Image is securely stored in MinIO storage.
          </div>
        </div>
      </div>
    </FormField>
  );
}

function InventoryContentInner() {
  const queryClient = useQueryClient();
  const { role, hasRole, hasPermission } = useAuth();
  const canModify =
    hasRole?.("super_admin") ||
    hasRole?.("inventory_manager") ||
    hasRole?.("system_admin") ||
    hasRole?.("branch_manager") ||
    role === "super_admin" ||
    role === "inventory_manager" ||
    role === "system_admin" ||
    role === "branch_manager" ||
    (typeof hasPermission === "function" && (hasPermission("*.*") || hasPermission("inventory.write"))) ||
    !role;

  // Load branches
  const { data: branchesData } = useQuery({
    queryKey: ["adminBranches"],
    queryFn: () => apiClient("/api/v1/catalog/branches").catch(() => []),
  });

  const ALL_BRANCHES_OPTION = React.useMemo(() => ({ value: "all", label: "All Branches" }), []);

  const branchOptions = React.useMemo(() => {
    if (!branchesData) return [ALL_BRANCHES_OPTION];
    const list = Array.isArray(branchesData)
      ? branchesData
      : Array.isArray(branchesData.items)
      ? branchesData.items
      : Array.isArray(branchesData.data)
      ? branchesData.data
      : [];
    const mapped = list.map((b) => ({
      value: b.branchId || b.id,
      label: b.name || "Unknown Branch",
    }));
    return [ALL_BRANCHES_OPTION, ...mapped];
  }, [branchesData, ALL_BRANCHES_OPTION]);

  const getInitialBranch = () => {
    if (typeof window !== "undefined") {
      const saved =
        localStorage.getItem("cinema_admin_inventory_branch") ||
        localStorage.getItem("cinema_admin_selected_branch");
      if (saved && saved !== "all") {
        return { value: saved, label: "Loading branch..." };
      }
    }
    return { value: "all", label: "All Branches" };
  };

  const [selectedBranchOption, setSelectedBranchOption] = React.useState(getInitialBranch);

  React.useEffect(() => {
    if (typeof window !== "undefined" && branchOptions.length > 1) {
      const saved =
        localStorage.getItem("cinema_admin_inventory_branch") ||
        localStorage.getItem("cinema_admin_selected_branch");
      if (saved && saved !== "all") {
        const found = branchOptions.find((b) => b.value === saved);
        if (found) {
          setSelectedBranchOption(found);
        }
      }
    }
  }, [branchOptions]);

  const handleBranchChange = React.useCallback(
    (opt) => {
      const option = opt || ALL_BRANCHES_OPTION;
      setSelectedBranchOption(option);
      if (typeof window !== "undefined") {
        localStorage.setItem("cinema_admin_inventory_branch", option.value);
        localStorage.setItem("cinema_admin_selected_branch", option.value);
      }
    },
    [ALL_BRANCHES_OPTION]
  );

  const activeBranchId = selectedBranchOption?.value || "all";

  const { data, isLoading, error, refetch, isFetching } = useQuery({
    queryKey: ["inventory", activeBranchId],
    queryFn: async () => {
      const realBranches = branchOptions.filter((b) => b.value !== "all");
      if (activeBranchId === "all") {
        if (realBranches.length === 0) return [];
        const results = await Promise.all(
          realBranches.map(async (b) => {
            try {
              const res = await apiClient(`/api/v1/admin/inventory/branches/${b.value}/stock`);
              const items = Array.isArray(res) ? res : res.items || res.data || [];
              return items.map((item) => ({
                ...item,
                branchId: b.value,
                branchName: b.label,
              }));
            } catch (err) {
              console.error(`Failed to fetch inventory for branch ${b.label}:`, err);
              return [];
            }
          })
        );
        return results.flat();
      } else {
        const res = await apiClient(`/api/v1/admin/inventory/branches/${activeBranchId}/stock`);
        const items = Array.isArray(res) ? res : res.items || res.data || [];
        const currentBranch = branchOptions.find((b) => b.value === activeBranchId);
        return items.map((item) => ({
          ...item,
          branchId: activeBranchId,
          branchName: currentBranch?.label || "Cinema Branch",
        }));
      }
    },
    enabled: activeBranchId !== "all" || branchOptions.length > 1,
  });

  const [notifications, setNotifications] = React.useState([]);

  // Adjust stock state
  const [modalOpen, setModalOpen] = React.useState(false);
  const [adjustAmount, setAdjustAmount] = React.useState("");
  const [adjustReason, setAdjustReason] = React.useState("");

  // Add Product state
  const [addModalOpen, setAddModalOpen] = React.useState(false);
  const [formName, setFormName] = React.useState("");
  const [formCategory, setFormCategory] = React.useState(CATEGORY_OPTIONS[0]);
  const [formPrice, setFormPrice] = React.useState("6.50");
  const [formSku, setFormSku] = React.useState("");
  const [formTargetBranch, setFormTargetBranch] = React.useState(ALL_BRANCHES_OPTION);
  const [formInitialStock, setFormInitialStock] = React.useState("100");
  const [formReorderThreshold, setFormReorderThreshold] = React.useState("20");
  const [formImageUrl, setFormImageUrl] = React.useState("");
  const [isUploadingAdd, setIsUploadingAdd] = React.useState(false);
  const [addUploadError, setAddUploadError] = React.useState("");
  const addFileInputRef = React.useRef(null);
  const [addError, setAddError] = React.useState("");

  // Edit Product state
  const [editModalOpen, setEditModalOpen] = React.useState(false);
  const [targetEditProduct, setTargetEditProduct] = React.useState(null);
  const [editName, setEditName] = React.useState("");
  const [editCategory, setEditCategory] = React.useState(CATEGORY_OPTIONS[0]);
  const [editPrice, setEditPrice] = React.useState("0");
  const [editActive, setEditActive] = React.useState(true);
  const [editImageUrl, setEditImageUrl] = React.useState("");
  const [isUploadingEdit, setIsUploadingEdit] = React.useState(false);
  const [editUploadError, setEditUploadError] = React.useState("");
  const editFileInputRef = React.useRef(null);
  const [editError, setEditError] = React.useState("");

  // Delete Product confirmation state
  const [deleteModalOpen, setDeleteModalOpen] = React.useState(false);
  const [targetDeleteProduct, setTargetDeleteProduct] = React.useState(null);
  const [deleteError, setDeleteError] = React.useState("");

  // Wastage / Spoilage state
  const [wastageModalOpen, setWastageModalOpen] = React.useState(false);
  const [targetWastageProduct, setTargetWastageProduct] = React.useState(null);
  const [wastageQuantity, setWastageQuantity] = React.useState("1");
  const [wastageReason, setWastageReason] = React.useState(WASTAGE_REASON_OPTIONS[0]);
  const [wastageUnitCost, setWastageUnitCost] = React.useState("0.00");
  const [wastageNotes, setWastageNotes] = React.useState("");
  const [wastageError, setWastageError] = React.useState("");

  const inventory = React.useMemo(() => {
    if (!data) return [];
    if (Array.isArray(data)) return data;
    if (Array.isArray(data.items)) return data.items;
    if (Array.isArray(data.data)) return data.data;
    return [];
  }, [data]);

  const lowStockCount = inventory.filter(
    (item) => (item.stockQuantity ?? item.stockLevel ?? 0) <= (item.reorderThreshold ?? 20)
  ).length;

  const { items, actions, filteredItemsCount, collectionProps, filterProps, paginationProps } =
    useCollection(inventory, {
      filtering: {
        empty: (
          <Box textAlign="center" color="inherit">
            <b>No F&B items found</b>
          </Box>
        ),
        noMatch: (
          <Box textAlign="center" color="inherit">
            <b>No matches found</b>
            <Button onClick={() => actions.setFiltering("")}>Clear filter</Button>
          </Box>
        ),
        filteringFunction: (item, filteringText) => {
          if (!filteringText) return true;
          const text = filteringText.toLowerCase();
          return (
            (item.productName ?? item.itemName ?? "").toLowerCase().includes(text) ||
            (item.category ?? "").toLowerCase().includes(text) ||
            (item.sku ?? "").toLowerCase().includes(text) ||
            (item.branchName ?? "").toLowerCase().includes(text)
          );
        },
      },
      pagination: { pageSize: 10 },
      sorting: {},
      selection: {
        trackBy: (item) => `${item.branchId || "global"}-${item.productId || item.inventoryId || item.sku}`,
      },
    });

  const selectedItem = collectionProps.selectedItems?.[0] || null;

  // --- 1. Adjust Stock Mutation ---
  const adjustMutation = useMutation({
    mutationFn: ({ branchId, productId, quantityDelta, reason }) =>
      apiClient(`/api/v1/admin/inventory/branches/${branchId}/products/${productId}/adjust`, {
        method: "POST",
        body: JSON.stringify({ quantityDelta, reason }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory"] });
      setModalOpen(false);
      setAdjustAmount("");
      setAdjustReason("");
      setNotifications([
        {
          type: "success",
          content: `Stock for ${selectedItem?.productName ?? selectedItem?.itemName}${
            selectedItem?.branchName ? ` (${selectedItem.branchName})` : ""
          } adjusted successfully.`,
          dismissible: true,
          id: "adjust-success-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
    onError: (err) => {
      setNotifications([
        {
          type: "error",
          content: `Failed to adjust stock: ${err.message}`,
          dismissible: true,
          id: "adjust-error-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
  });

  const handleAdjustStock = () => {
    if (!selectedItem) return;
    const amount = parseInt(adjustAmount, 10);
    if (isNaN(amount)) return;
    const productId = selectedItem.productId ?? selectedItem.id;
    const branchId = selectedItem.branchId || (activeBranchId !== "all" ? activeBranchId : null);
    if (!productId || !branchId) return;
    adjustMutation.mutate({
      branchId,
      productId,
      quantityDelta: amount,
      reason: adjustReason || "Manual adjustment",
    });
  };

  // Helper for image upload to MinIO via Catalog.Api
  const handleImageUpload = async (event, setUrl, setIsUploading, setError) => {
    const file = event.target.files?.[0];
    if (!file) return;
    event.target.value = "";
    setIsUploading(true);
    setError("");
    try {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("category", "concessions");
      const res = await apiClient("/api/v1/media/upload", {
        method: "POST",
        body: formData,
      });
      if (res && res.publicUrl) {
        setUrl(res.publicUrl);
      } else {
        throw new Error("Upload response missing publicUrl");
      }
    } catch (err) {
      setError(err.message || "Failed to upload product image to MinIO.");
    } finally {
      setIsUploading(false);
    }
  };

  // --- 2. Add Product Mutation ---
  const openAddModal = () => {
    setFormName("");
    setFormCategory(CATEGORY_OPTIONS[0]);
    setFormPrice("6.50");
    setFormSku("");
    setFormTargetBranch(selectedBranchOption || ALL_BRANCHES_OPTION);
    setFormInitialStock("100");
    setFormReorderThreshold("20");
    setFormImageUrl("");
    setAddUploadError("");
    setAddError("");
    setAddModalOpen(true);
  };

  const addProductMutation = useMutation({
    mutationFn: (newProduct) =>
      apiClient("/api/v1/admin/inventory/products", {
        method: "POST",
        body: JSON.stringify(newProduct),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory"] });
      setAddModalOpen(false);
      setNotifications([
        {
          type: "success",
          content: `Product "${formName}" created successfully.`,
          dismissible: true,
          id: "add-prod-success-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
    onError: (err) => {
      setAddError(err.message || "Failed to create product.");
    },
  });

  const handleSaveProduct = () => {
    if (!formName || !formName.trim()) {
      setAddError("Product name is required.");
      return;
    }
    const price = parseFloat(formPrice);
    if (isNaN(price) || price < 0) {
      setAddError("Unit price must be a valid number (e.g. 5.00).");
      return;
    }
    const initialStock = parseInt(formInitialStock, 10);
    const reorder = parseInt(formReorderThreshold, 10);

    setAddError("");
    const branchVal = formTargetBranch?.value;
    const payload = {
      name: formName.trim(),
      category: formCategory?.value || "Combos",
      unitPrice: price,
      sku: formSku ? formSku.trim() : undefined,
      branchId: branchVal && branchVal !== "all" ? branchVal : undefined,
      initialStock: isNaN(initialStock) ? 100 : initialStock,
      reorderThreshold: isNaN(reorder) ? 20 : reorder,
      imageUrl: formImageUrl && formImageUrl.trim() ? formImageUrl.trim() : undefined,
    };

    addProductMutation.mutate(payload);
  };

  // --- 3. Edit Product Mutation ---
  const openEditModal = (item) => {
    const prod = item || selectedItem;
    if (!prod) return;
    setTargetEditProduct(prod);
    setEditName(prod.productName ?? prod.itemName ?? prod.name ?? "");
    const foundCat = CATEGORY_OPTIONS.find((c) => c.value === prod.category) || {
      value: prod.category || "Combos",
      label: prod.category || "Combos",
    };
    setEditCategory(foundCat);
    setEditPrice(String(prod.unitPrice ?? prod.price ?? 0));
    setEditActive(prod.isActive !== false);
    setEditImageUrl(prod.imageUrl || "");
    setEditUploadError("");
    setEditError("");
    setEditModalOpen(true);
  };

  const editProductMutation = useMutation({
    mutationFn: ({ productId, payload }) =>
      apiClient(`/api/v1/admin/inventory/products/${productId}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory"] });
      setEditModalOpen(false);
      setNotifications([
        {
          type: "success",
          content: `Product "${editName}" updated successfully.`,
          dismissible: true,
          id: "edit-prod-success-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
    onError: (err) => {
      setEditError(err.message || "Failed to update product.");
    },
  });

  const handleSaveEditProduct = () => {
    if (!targetEditProduct) return;
    if (!editName || !editName.trim()) {
      setEditError("Product name is required.");
      return;
    }
    const price = parseFloat(editPrice);
    if (isNaN(price) || price < 0) {
      setEditError("Unit price must be a valid non-negative number.");
      return;
    }
    const productId = targetEditProduct.productId ?? targetEditProduct.id;
    editProductMutation.mutate({
      productId,
      payload: {
        name: editName.trim(),
        category: editCategory?.value || "Combos",
        unitPrice: price,
        isActive: editActive,
        imageUrl: editImageUrl && editImageUrl.trim() ? editImageUrl.trim() : undefined,
      },
    });
  };

  // --- 4. Delete Product Mutation ---
  const openDeleteModal = (item) => {
    const prod = item || selectedItem;
    if (!prod) return;
    setTargetDeleteProduct(prod);
    setDeleteError("");
    setDeleteModalOpen(true);
  };

  const deleteProductMutation = useMutation({
    mutationFn: (productId) =>
      apiClient(`/api/v1/admin/inventory/products/${productId}`, {
        method: "DELETE",
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory"] });
      setDeleteModalOpen(false);
      setNotifications([
        {
          type: "success",
          content: `Product "${targetDeleteProduct?.productName ?? targetDeleteProduct?.itemName}" deleted/deactivated successfully.`,
          dismissible: true,
          id: "del-prod-success-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
    onError: (err) => {
      setDeleteError(err.message || "Failed to delete product.");
    },
  });

  const handleDeleteProduct = () => {
    if (!targetDeleteProduct) return;
    const productId = targetDeleteProduct.productId ?? targetDeleteProduct.id;
    deleteProductMutation.mutate(productId);
  };

  // --- 5. Toggle Availability (1-Click Mark In / Out of Stock) ---
  const toggleAvailabilityMutation = useMutation({
    mutationFn: ({ branchId, productId, isOutOfStock }) =>
      apiClient(`/api/v1/admin/inventory/branches/${branchId}/products/${productId}/availability`, {
        method: "PATCH",
        body: JSON.stringify({ isOutOfStock }),
      }),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: ["inventory"] });
      setNotifications([
        {
          type: "success",
          content: `Product availability updated to: ${vars.isOutOfStock ? "Out of Stock" : "In Stock"}.`,
          dismissible: true,
          id: "avail-success-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
    onError: (err) => {
      setNotifications([
        {
          type: "error",
          content: `Failed to update availability: ${err.message}`,
          dismissible: true,
          id: "avail-err-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
  });

  const handleToggleAvailability = (item) => {
    const prod = item || selectedItem;
    if (!prod) return;
    const branchId = prod.branchId || (activeBranchId !== "all" ? activeBranchId : null);
    const productId = prod.productId ?? prod.id;
    if (!branchId || !productId) {
      setNotifications([
        {
          type: "error",
          content: "Please select a specific branch or product row to update availability.",
          dismissible: true,
          id: "avail-err-no-branch-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
      return;
    }
    const nextOutOfStock = !prod.isOutOfStock;
    toggleAvailabilityMutation.mutate({ branchId, productId, isOutOfStock: nextOutOfStock });
  };

  // --- 6. Log Wastage / Spoilage Mutation ---
  const openWastageModal = (item) => {
    const prod = item || selectedItem;
    if (!prod) return;
    setTargetWastageProduct(prod);
    setWastageQuantity("1");
    setWastageReason(WASTAGE_REASON_OPTIONS[0]);
    const price = Number(prod.unitPrice ?? prod.price ?? 0);
    setWastageUnitCost(price > 0 ? (price * 0.4).toFixed(2) : "0.00");
    setWastageNotes("");
    setWastageError("");
    setWastageModalOpen(true);
  };

  const wastageMutation = useMutation({
    mutationFn: ({ branchId, payload }) =>
      apiClient(`/api/v1/admin/inventory/branches/${branchId}/wastage`, {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory"] });
      setWastageModalOpen(false);
      setNotifications([
        {
          type: "success",
          content: `Recorded wastage of ${wastageQuantity} units for ${
            targetWastageProduct?.productName ?? targetWastageProduct?.itemName
          }.`,
          dismissible: true,
          id: "wastage-success-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    },
    onError: (err) => {
      setWastageError(err.message || "Failed to log wastage.");
    },
  });

  const handleSaveWastage = () => {
    if (!targetWastageProduct) return;
    const qty = parseInt(wastageQuantity, 10);
    if (isNaN(qty) || qty <= 0) {
      setWastageError("Quantity must be a positive integer.");
      return;
    }
    const branchId = targetWastageProduct.branchId || (activeBranchId !== "all" ? activeBranchId : null);
    const productId = targetWastageProduct.productId ?? targetWastageProduct.id;
    if (!branchId || !productId) {
      setWastageError("Please select a specific cinema branch row to record wastage.");
      return;
    }
    const cost = parseFloat(wastageUnitCost) || 0;
    wastageMutation.mutate({
      branchId,
      payload: {
        productId,
        quantity: qty,
        reason: wastageReason.value,
        unitCost: cost,
        notes: wastageNotes || undefined,
      },
    });
  };

  return (
    <ContentLayout
      header={
        <Header
          variant="h1"
          description="Manage cinema concessions, combos, beverages, popcorn, and stock levels across branches."
          actions={
            <SpaceBetween direction="horizontal" size="xs">
              <Button iconName="refresh" onClick={() => refetch()} loading={isFetching}>
                Refresh
              </Button>
              <Button
                disabled={!selectedItem}
                onClick={() => handleToggleAvailability(selectedItem)}
                loading={toggleAvailabilityMutation.isPending}
              >
                {selectedItem?.isOutOfStock ? "Mark In Stock" : "Mark Out of Stock"}
              </Button>
              <Button disabled={!selectedItem} onClick={() => openWastageModal(selectedItem)}>
                Log Wastage
              </Button>
              <Button disabled={!selectedItem} onClick={() => setModalOpen(true)}>
                Adjust Stock
              </Button>
              <Button disabled={!selectedItem || !canModify} onClick={() => openEditModal(selectedItem)}>
                Edit Product
              </Button>
              <Button disabled={!selectedItem || !canModify} onClick={() => openDeleteModal(selectedItem)}>
                Delete Product
              </Button>
              <Button variant="primary" iconName="add-plus" onClick={openAddModal} disabled={!canModify}>
                Add Product
              </Button>
            </SpaceBetween>
          }
        >
          F&amp;B Inventory
        </Header>
      }
    >
      <SpaceBetween size="l">
        <Container>
          <div style={{ display: "flex", alignItems: "center", gap: "16px", flexWrap: "wrap" }}>
            <span style={{ fontWeight: 600, fontSize: "14px", color: "#16191f" }}>
              Cinema Branch:
            </span>
            <div style={{ width: "340px", maxWidth: "100%" }}>
              <Select
                selectedOption={selectedBranchOption}
                onChange={({ detail }) => handleBranchChange(detail.selectedOption)}
                options={branchOptions}
                placeholder="Select cinema branch..."
              />
            </div>
            {selectedBranchOption && (
              <Badge color="blue">
                {inventory.length} Products
              </Badge>
            )}
            {lowStockCount > 0 && (
              <Badge color="red">
                {lowStockCount} Low Stock Alert{lowStockCount > 1 ? "s" : ""}
              </Badge>
            )}
            {selectedBranchOption?.value !== "all" && (
              <Button variant="link" onClick={() => handleBranchChange(ALL_BRANCHES_OPTION)}>
                Show all branches
              </Button>
            )}
          </div>
        </Container>

        {lowStockCount > 0 && (
          <Alert type="warning" header="Low Stock Warning">
            {lowStockCount} item{lowStockCount > 1 ? "s are" : " is"} at or below reorder threshold. Please replenish stock.
          </Alert>
        )}

        {notifications.length > 0 && <Flashbar items={notifications} />}
        {error && (
          <Alert type="error" header="Failed to load inventory">
            {error.message}
          </Alert>
        )}

        <Table
          {...collectionProps}
          selectionType="single"
          columnDefinitions={[
            ...(activeBranchId === "all"
              ? [
                  {
                    id: "branchName",
                    header: "Branch",
                    sortingField: "branchName",
                    cell: (item) => <Badge color="blue">{item.branchName || "Branch"}</Badge>,
                  },
                ]
              : []),
            {
              id: "image",
              header: "Item",
              cell: (item) => (
                <FnbThumbnail
                  name={item.productName ?? item.itemName}
                  category={item.category}
                  imageUrl={item.imageUrl}
                />
              ),
            },
            {
              id: "sku",
              header: "SKU",
              sortingField: "sku",
              cell: (item) => <span style={{ fontFamily: "monospace", fontSize: "12px" }}>{item.sku ?? "-"}</span>,
            },
            {
              id: "productName",
              header: "Product Name",
              sortingField: "productName",
              cell: (item) => (
                <span style={{ fontWeight: 600 }}>
                  {item.productName ?? item.itemName ?? "-"}
                </span>
              ),
              isRowHeader: true,
            },
            {
              id: "category",
              header: "Category",
              sortingField: "category",
              cell: (item) => (
                <Badge color={item.category === "Combos" ? "blue" : item.category === "Popcorn" ? "green" : "grey"}>
                  {item.category ?? "-"}
                </Badge>
              ),
            },
            {
              id: "unitPrice",
              header: "Price",
              sortingField: "unitPrice",
              cell: (item) => `$${Number(item.unitPrice ?? item.price ?? 0).toFixed(2)}`,
            },
            {
              id: "stockQuantity",
              header: "In Stock",
              sortingField: "stockQuantity",
              cell: (item) => {
                const stock = item.stockQuantity ?? item.stockLevel ?? 0;
                const threshold = item.reorderThreshold ?? 20;
                const isLow = stock <= threshold;
                return (
                  <span
                    style={{
                      color: isLow ? "#d13212" : "#0972d3",
                      fontWeight: 700,
                      fontSize: "14px",
                    }}
                  >
                    {stock}
                  </span>
                );
              },
            },
            {
              id: "reorderThreshold",
              header: "Reorder Level",
              sortingField: "reorderThreshold",
              cell: (item) => `${item.reorderThreshold ?? 20} units`,
            },
            {
              id: "status",
              header: "Stock Status",
              cell: (item) => {
                if (item.isOutOfStock) {
                  return <StatusIndicator type="error">Out of Stock (Manual)</StatusIndicator>;
                }
                const stock = item.stockQuantity ?? item.stockLevel ?? 0;
                const threshold = item.reorderThreshold ?? 20;
                if (stock <= 0) return <StatusIndicator type="error">Out of Stock</StatusIndicator>;
                if (stock <= threshold) return <StatusIndicator type="warning">Low Stock</StatusIndicator>;
                return <StatusIndicator type="success">In Stock</StatusIndicator>;
              },
            },
            {
              id: "actions",
              header: "Quick Actions",
              cell: (item) => (
                <SpaceBetween direction="horizontal" size="xxs">
                  <Button
                    variant="inline-icon"
                    iconName={item.isOutOfStock ? "status-positive" : "status-negative"}
                    ariaLabel={item.isOutOfStock ? "Mark In Stock" : "Mark Out of Stock"}
                    title={item.isOutOfStock ? "Mark In Stock" : "Mark Out of Stock"}
                    onClick={() => handleToggleAvailability(item)}
                  />
                  <Button
                    variant="inline-icon"
                    iconName="file"
                    ariaLabel="Log Spoilage / Wastage"
                    title="Log Spoilage / Wastage"
                    onClick={() => openWastageModal(item)}
                  />
                  <Button
                    variant="inline-icon"
                    iconName="edit"
                    ariaLabel="Edit Product"
                    title="Edit Product"
                    onClick={() => openEditModal(item)}
                  />
                  <Button
                    variant="inline-icon"
                    iconName="remove"
                    ariaLabel="Delete Product"
                    title="Delete Product"
                    onClick={() => openDeleteModal(item)}
                  />
                </SpaceBetween>
              ),
            },
          ]}
          items={items}
          loading={isLoading}
          loadingText="Loading branch inventory..."
          filter={
            <TextFilter
              {...filterProps}
              filteringPlaceholder="Filter by product name, category, or SKU..."
              countText={`${filteredItemsCount} ${filteredItemsCount === 1 ? "match" : "matches"}`}
            />
          }
          pagination={<Pagination {...paginationProps} />}
          empty={
            <Box textAlign="center" color="inherit">
              <b>No inventory found</b>
              <Box padding={{ bottom: "s" }} variant="p" color="inherit">
                Select a cinema branch to view available F&amp;B products and stock.
              </Box>
            </Box>
          }
        />

        {/* Modal 1: Adjust Stock */}
        <Modal
          visible={modalOpen}
          onDismiss={() => setModalOpen(false)}
          header={`Adjust Stock: ${selectedItem?.productName ?? selectedItem?.itemName}${
            selectedItem?.branchName ? ` (${selectedItem.branchName})` : ""
          }`}
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button variant="link" onClick={() => setModalOpen(false)}>
                  Cancel
                </Button>
                <Button
                  variant="primary"
                  onClick={handleAdjustStock}
                  loading={adjustMutation.isPending}
                  disabled={!adjustAmount || isNaN(parseInt(adjustAmount, 10))}
                >
                  Save
                </Button>
              </SpaceBetween>
            </Box>
          }
        >
          <SpaceBetween size="m">
            <FormField
              label="Amount to Adjust"
              description="Use positive numbers to add stock (+), negative numbers to reduce (-)."
            >
              <Input
                type="number"
                value={adjustAmount}
                onChange={({ detail }) => setAdjustAmount(detail.value)}
                placeholder="e.g. 50 or -5"
              />
            </FormField>
            <FormField label="Reason">
              <Input
                value={adjustReason}
                onChange={({ detail }) => setAdjustReason(detail.value)}
                placeholder="e.g. Received weekly shipment, physical count reconciliation"
              />
            </FormField>
          </SpaceBetween>
        </Modal>

        {/* Modal 2: Add Product */}
        <Modal
          visible={addModalOpen}
          onDismiss={() => setAddModalOpen(false)}
          header="Add New F&B Product"
          closeAriaLabel="Close modal"
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button variant="link" onClick={() => setAddModalOpen(false)}>
                  Cancel
                </Button>
                <Button
                  variant="primary"
                  onClick={handleSaveProduct}
                  loading={addProductMutation.isPending}
                >
                  Create Product
                </Button>
              </SpaceBetween>
            </Box>
          }
        >
          <SpaceBetween size="m">
            {addError && (
              <Alert type="error" dismissible onDismiss={() => setAddError("")}>
                {addError}
              </Alert>
            )}

            <FnbImageUploadControl
              imageUrl={formImageUrl}
              onChangeImageUrl={setFormImageUrl}
              onFileSelect={(e) =>
                handleImageUpload(e, setFormImageUrl, setIsUploadingAdd, setAddUploadError)
              }
              isUploading={isUploadingAdd}
              uploadError={addUploadError}
              fileInputRef={addFileInputRef}
              name={formName}
              category={formCategory?.value}
            />

            <FormField label="Product Name" description="Display name for concessions menu and checkout">
              <Input
                value={formName}
                onChange={({ detail }) => setFormName(detail.value)}
                placeholder="e.g. Sweet Caramel Popcorn (L) or Deluxe Combo"
              />
            </FormField>

            <ColumnLayout columns={2}>
              <FormField label="Category">
                <Select
                  selectedOption={formCategory}
                  onChange={({ detail }) => setFormCategory(detail.selectedOption)}
                  options={CATEGORY_OPTIONS}
                />
              </FormField>

              <FormField label="Price ($ USD)">
                <Input
                  type="number"
                  value={formPrice}
                  onChange={({ detail }) => setFormPrice(detail.value)}
                  placeholder="e.g. 5.50"
                />
              </FormField>
            </ColumnLayout>

            <ColumnLayout columns={2}>
              <FormField label="SKU (Stock Keeping Unit)" description="Leave blank to auto-generate">
                <Input
                  value={formSku}
                  onChange={({ detail }) => setFormSku(detail.value)}
                  placeholder="e.g. POP-CAR-LG"
                />
              </FormField>

              <FormField label="Cinema Branch" description="Select target branch or all branches">
                <Select
                  selectedOption={formTargetBranch}
                  onChange={({ detail }) => setFormTargetBranch(detail.selectedOption)}
                  options={branchOptions}
                />
              </FormField>
            </ColumnLayout>

            <ColumnLayout columns={2}>
              <FormField label="Initial Stock Quantity" description="Units initially stocked">
                <Input
                  type="number"
                  value={formInitialStock}
                  onChange={({ detail }) => setFormInitialStock(detail.value)}
                  placeholder="100"
                />
              </FormField>

              <FormField label="Reorder Threshold" description="Triggers low stock alert when below this count">
                <Input
                  type="number"
                  value={formReorderThreshold}
                  onChange={({ detail }) => setFormReorderThreshold(detail.value)}
                  placeholder="20"
                />
              </FormField>
            </ColumnLayout>
          </SpaceBetween>
        </Modal>

        {/* Modal 3: Edit Product */}
        <Modal
          visible={editModalOpen}
          onDismiss={() => setEditModalOpen(false)}
          header={`Edit Product: ${targetEditProduct?.productName ?? targetEditProduct?.itemName ?? "Product"}`}
          closeAriaLabel="Close modal"
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button variant="link" onClick={() => setEditModalOpen(false)}>
                  Cancel
                </Button>
                <Button
                  variant="primary"
                  onClick={handleSaveEditProduct}
                  loading={editProductMutation.isPending}
                >
                  Save Changes
                </Button>
              </SpaceBetween>
            </Box>
          }
        >
          <SpaceBetween size="m">
            {editError && (
              <Alert type="error" dismissible onDismiss={() => setEditError("")}>
                {editError}
              </Alert>
            )}

            <FnbImageUploadControl
              imageUrl={editImageUrl}
              onChangeImageUrl={setEditImageUrl}
              onFileSelect={(e) =>
                handleImageUpload(e, setEditImageUrl, setIsUploadingEdit, setEditUploadError)
              }
              isUploading={isUploadingEdit}
              uploadError={editUploadError}
              fileInputRef={editFileInputRef}
              name={editName}
              category={editCategory?.value}
            />

            <FormField label="Product Name">
              <Input
                value={editName}
                onChange={({ detail }) => setEditName(detail.value)}
                placeholder="Product name"
              />
            </FormField>

            <ColumnLayout columns={2}>
              <FormField label="Category">
                <Select
                  selectedOption={editCategory}
                  onChange={({ detail }) => setEditCategory(detail.selectedOption)}
                  options={CATEGORY_OPTIONS}
                />
              </FormField>

              <FormField label="Price ($ USD)">
                <Input
                  type="number"
                  value={editPrice}
                  onChange={({ detail }) => setEditPrice(detail.value)}
                  placeholder="e.g. 6.50"
                />
              </FormField>
            </ColumnLayout>

            <FormField label="Product Status">
              <Toggle
                checked={editActive}
                onChange={({ detail }) => setEditActive(detail.checked)}
              >
                {editActive ? "Active (Listed on POS & Mobile App)" : "Inactive / Archived"}
              </Toggle>
            </FormField>
          </SpaceBetween>
        </Modal>

        {/* Modal 4: Delete Product Confirmation */}
        <Modal
          visible={deleteModalOpen}
          onDismiss={() => setDeleteModalOpen(false)}
          header={`Delete Product: ${targetDeleteProduct?.productName ?? targetDeleteProduct?.itemName ?? ""}`}
          closeAriaLabel="Close modal"
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button variant="link" onClick={() => setDeleteModalOpen(false)}>
                  Cancel
                </Button>
                <Button
                  variant="primary"
                  onClick={handleDeleteProduct}
                  loading={deleteProductMutation.isPending}
                >
                  Confirm Delete
                </Button>
              </SpaceBetween>
            </Box>
          }
        >
          <SpaceBetween size="m">
            {deleteError && (
              <Alert type="error" dismissible onDismiss={() => setDeleteError("")}>
                {deleteError}
              </Alert>
            )}
            <Alert type="warning" header="Are you sure you want to remove this product?">
              This will remove <b>{targetDeleteProduct?.productName ?? targetDeleteProduct?.itemName}</b> (SKU: {targetDeleteProduct?.sku ?? "N/A"}).
              If previous customer orders exist with this product, it will be safely deactivated and hidden to protect transaction history.
            </Alert>
          </SpaceBetween>
        </Modal>

        {/* Modal 5: Log Spoilage / Wastage */}
        <Modal
          visible={wastageModalOpen}
          onDismiss={() => setWastageModalOpen(false)}
          header={`Log Wastage / Spoilage: ${targetWastageProduct?.productName ?? targetWastageProduct?.itemName ?? ""}`}
          closeAriaLabel="Close modal"
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button variant="link" onClick={() => setWastageModalOpen(false)}>
                  Cancel
                </Button>
                <Button
                  variant="primary"
                  onClick={handleSaveWastage}
                  loading={wastageMutation.isPending}
                >
                  Record Wastage
                </Button>
              </SpaceBetween>
            </Box>
          }
        >
          <SpaceBetween size="m">
            {wastageError && (
              <Alert type="error" dismissible onDismiss={() => setWastageError("")}>
                {wastageError}
              </Alert>
            )}

            <ColumnLayout columns={2}>
              <FormField label="Quantity Wasted / Spoiled" description="Units to write off">
                <Input
                  type="number"
                  value={wastageQuantity}
                  onChange={({ detail }) => setWastageQuantity(detail.value)}
                  placeholder="1"
                />
              </FormField>

              <FormField label="Reason">
                <Select
                  selectedOption={wastageReason}
                  onChange={({ detail }) => setWastageReason(detail.selectedOption)}
                  options={WASTAGE_REASON_OPTIONS}
                />
              </FormField>
            </ColumnLayout>

            <FormField label="Estimated Unit Cost ($ USD)" description="Cost per unit to calculate financial loss">
              <Input
                type="number"
                value={wastageUnitCost}
                onChange={({ detail }) => setWastageUnitCost(detail.value)}
                placeholder="0.00"
              />
            </FormField>

            <FormField label="Incident Notes (Optional)" description="e.g. Freezer breakdown in storage room 2">
              <Input
                value={wastageNotes}
                onChange={({ detail }) => setWastageNotes(detail.value)}
                placeholder="Describe reason for wastage"
              />
            </FormField>
          </SpaceBetween>
        </Modal>
      </SpaceBetween>
    </ContentLayout>
  );
}

export default function InventoryContent() {
  return (
    <QueryWrapper>
      <InventoryContentInner />
    </QueryWrapper>
  );
}
