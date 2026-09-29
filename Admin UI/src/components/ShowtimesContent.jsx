import * as React from "react";
import ContentLayout from "@cloudscape-design/components/content-layout";
import Header from "@cloudscape-design/components/header";
import Table from "@cloudscape-design/components/table";
import Button from "@cloudscape-design/components/button";
import SpaceBetween from "@cloudscape-design/components/space-between";
import Badge from "@cloudscape-design/components/badge";
import StatusIndicator from "@cloudscape-design/components/status-indicator";
import TextFilter from "@cloudscape-design/components/text-filter";
import Pagination from "@cloudscape-design/components/pagination";
import Box from "@cloudscape-design/components/box";
import Alert from "@cloudscape-design/components/alert";
import Modal from "@cloudscape-design/components/modal";
import FormField from "@cloudscape-design/components/form-field";
import Input from "@cloudscape-design/components/input";
import Select from "@cloudscape-design/components/select";
import Flashbar from "@cloudscape-design/components/flashbar";
import ColumnLayout from "@cloudscape-design/components/column-layout";
import Container from "@cloudscape-design/components/container";
import { useCollection } from "@cloudscape-design/collection-hooks";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient, API_BASE_URL } from "../lib/apiClient";
import { QueryWrapper } from "./QueryWrapper";



// ─── Status helpers ────────────────────────────────────────────────
function renderShowtimeStatus(status) {
  const s = String(status || "scheduled").toLowerCase().replace(/[\s-]+/g, "_");
  if (s === "active" || s === "now_playing") {
    return <StatusIndicator type="success">Active</StatusIndicator>;
  }
  if (s === "scheduled") {
    return <StatusIndicator type="info">Scheduled</StatusIndicator>;
  }
  if (s === "cancelled") {
    return <StatusIndicator type="error">Cancelled</StatusIndicator>;
  }
  if (s === "completed" || s === "ended") {
    return <StatusIndicator type="stopped">Completed</StatusIndicator>;
  }
  return <StatusIndicator type="info">{status || "Unknown"}</StatusIndicator>;
}

function formatDateTime(iso) {
  if (!iso) return "-";
  try {
    const d = new Date(iso);
    if (isNaN(d.getTime())) return iso;
    return d.toLocaleString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return iso;
  }
}

function formatPrice(price) {
  if (price == null) return "-";
  return `$${Number(price).toFixed(2)}`;
}

// ─── CSV validation status badges ──────────────────────────────────
function renderCsvRowStatus(status) {
  const s = String(status || "").toLowerCase();
  if (s === "valid") return <StatusIndicator type="success">Valid</StatusIndicator>;
  if (s === "conflict") return <StatusIndicator type="warning">Conflict</StatusIndicator>;
  if (s === "invalid_movie") return <StatusIndicator type="error">Invalid Movie</StatusIndicator>;
  if (s === "invalid_auditorium") return <StatusIndicator type="error">Invalid Auditorium</StatusIndicator>;
  if (s === "error" || s === "invalid") return <StatusIndicator type="error">Error</StatusIndicator>;
  return <StatusIndicator type="info">{status || "Unknown"}</StatusIndicator>;
}

// ─── Inner component (inside QueryWrapper) ─────────────────────────
function ShowtimesContentInner() {
  const queryClient = useQueryClient();

  // Fetch showtimes
  const { data, isLoading, error, refetch, isFetching } = useQuery({
    queryKey: ["showtimesCatalog"],
    queryFn: () => apiClient("/api/v1/admin/showtimes").catch(() => apiClient("/api/v1/catalog/showtimes")),
  });

  // Fetch branches
  const { data: branchesData } = useQuery({
    queryKey: ["adminBranchesForShowtimes"],
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

  const branchMap = React.useMemo(() => {
    const map = {};
    if (!branchesData) return map;
    const list = Array.isArray(branchesData)
      ? branchesData
      : Array.isArray(branchesData.items)
      ? branchesData.items
      : Array.isArray(branchesData.data)
      ? branchesData.data
      : [];
    list.forEach((b) => {
      const id = b.branchId || b.id;
      if (id) map[id] = b.name;
    });
    return map;
  }, [branchesData]);

  const getInitialBranch = () => {
    if (typeof window !== "undefined") {
      const saved =
        localStorage.getItem("cinema_admin_showtimes_branch") ||
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
        localStorage.getItem("cinema_admin_showtimes_branch") ||
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
        localStorage.setItem("cinema_admin_showtimes_branch", option.value);
        localStorage.setItem("cinema_admin_selected_branch", option.value);
      }
    },
    [ALL_BRANCHES_OPTION]
  );

  // Fetch movies for the Add form dropdown
  const { data: moviesData } = useQuery({
    queryKey: ["moviesCatalog"],
    queryFn: () => apiClient("/api/v1/catalog/movies"),
  });

  // Fetch auditoriums for the Add/Edit form dropdown
  const { data: auditoriumsData } = useQuery({
    queryKey: ["adminAuditoriums"],
    queryFn: () => apiClient("/api/v1/admin/auditoriums").catch(() => []),
  });

  const movieMap = React.useMemo(() => {
    const map = {};
    if (!moviesData) return map;
    const list = Array.isArray(moviesData)
      ? moviesData
      : Array.isArray(moviesData.items)
      ? moviesData.items
      : Array.isArray(moviesData.data)
      ? moviesData.data
      : [];
    list.forEach((m) => {
      const id = m.movieId || m.id;
      if (id) map[id] = m.title;
    });
    return map;
  }, [moviesData]);

  const movieOptions = React.useMemo(() => {
    if (!moviesData) return [];
    const list = Array.isArray(moviesData)
      ? moviesData
      : Array.isArray(moviesData.items)
      ? moviesData.items
      : Array.isArray(moviesData.data)
      ? moviesData.data
      : [];
    return list.map((m) => ({
      value: m.movieId || m.id || m.title,
      label: m.title || "Untitled",
    }));
  }, [moviesData]);

  const auditoriums = React.useMemo(() => {
    if (!auditoriumsData) return [];
    if (Array.isArray(auditoriumsData)) return auditoriumsData;
    if (Array.isArray(auditoriumsData.items)) return auditoriumsData.items;
    return [];
  }, [auditoriumsData]);

  const auditoriumOptions = React.useMemo(() => {
    return auditoriums.map((a) => ({
      value: a.auditoriumId || a.id,
      label: `${a.name || a.auditoriumName || "Auditorium"}${
        a.branchId && branchMap[a.branchId] ? ` (${branchMap[a.branchId]})` : ""
      }`,
    }));
  }, [auditoriums, branchMap]);

  // Local state
  const [localShowtimes, setLocalShowtimes] = React.useState(null);
  const [notifications, setNotifications] = React.useState([]);

  // Modal visibility
  const [addModalOpen, setAddModalOpen] = React.useState(false);
  const [editModalOpen, setEditModalOpen] = React.useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = React.useState(false);
  const [csvModalOpen, setCsvModalOpen] = React.useState(false);

  // --- Form states ---
  const [formBranch, setFormBranch] = React.useState(null);
  const [formMovie, setFormMovie] = React.useState(null);
  const [formAuditorium, setFormAuditorium] = React.useState(null);
  const [formStartTime, setFormStartTime] = React.useState("");
  const [formBasePrice, setFormBasePrice] = React.useState("");
  const [formError, setFormError] = React.useState("");

  // Filtered auditoriums for the form based on formBranch
  const formAuditoriumOptions = React.useMemo(() => {
    const list =
      formBranch?.value && formBranch.value !== "all"
        ? auditoriums.filter((a) => a.branchId === formBranch.value)
        : auditoriums;
    return list.map((a) => ({
      value: a.auditoriumId || a.id,
      label: `${a.name || "Auditorium"}${
        a.branchId && branchMap[a.branchId] ? ` (${branchMap[a.branchId]})` : ""
      }`,
    }));
  }, [auditoriums, formBranch, branchMap]);

  // --- CSV states ---
  const [csvFile, setCsvFile] = React.useState(null);
  const [csvFileName, setCsvFileName] = React.useState("");
  const [csvValidationResults, setCsvValidationResults] = React.useState(null);
  const [csvValidating, setCsvValidating] = React.useState(false);
  const [csvCommitting, setCsvCommitting] = React.useState(false);
  const [csvError, setCsvError] = React.useState("");

  const showtimes = React.useMemo(() => {
    if (!data) return [];
    if (Array.isArray(data)) return data;
    if (Array.isArray(data.items)) return data.items;
    if (Array.isArray(data.data)) return data.data;
    return [];
  }, [data]);

  const filteredShowtimesByBranch = React.useMemo(() => {
    if (!selectedBranchOption || selectedBranchOption.value === "all") {
      return showtimes;
    }
    return showtimes.filter((st) => st.branchId === selectedBranchOption.value);
  }, [showtimes, selectedBranchOption]);

  const {
    items,
    actions,
    filteredItemsCount,
    collectionProps,
    filterProps,
    paginationProps,
  } = useCollection(filteredShowtimesByBranch, {
    filtering: {
      empty: (
        <Box textAlign="center" color="inherit">
          <b>No showtimes found</b>
          <Box padding={{ bottom: "s" }} variant="p" color="inherit">
            No showtimes currently scheduled. Click "+ Add Showtime" to create one.
          </Box>
        </Box>
      ),
      noMatch: (
        <Box textAlign="center" color="inherit">
          <SpaceBetween size="xxs">
            <b>No matches found</b>
            <Box variant="p" color="inherit">
              We couldn't find any showtime matching your search criteria.
            </Box>
            <Button onClick={() => actions.setFiltering("")}>Clear filter</Button>
          </SpaceBetween>
        </Box>
      ),
      filteringFunction: (item, filteringText) => {
        if (!filteringText) return true;
        const text = filteringText.toLowerCase();
        const title = (item.movieTitle || item.title || movieMap[item.movieId] || "").toLowerCase();
        const auditorium = (item.auditoriumName || item.auditorium || "").toLowerCase();
        const branch = (branchMap[item.branchId] || item.branchName || "").toLowerCase();
        const status = (item.status || "").toLowerCase();
        return title.includes(text) || auditorium.includes(text) || branch.includes(text) || status.includes(text);
      },
    },
    pagination: { pageSize: 10 },
    sorting: {},
    selection: { trackBy: "showtimeId" },
  });

  const selectedItem = collectionProps.selectedItems?.[0] || null;
  const selectedCount = collectionProps.selectedItems?.length || 0;

  // --- Add Modal ---
  const openAddModal = () => {
    const defaultBranch =
      selectedBranchOption?.value !== "all"
        ? selectedBranchOption
        : branchOptions.find((b) => b.value !== "all") || null;
    setFormBranch(defaultBranch);
    setFormMovie(movieOptions[0] || null);

    const branchAuds = auditoriums.filter(
      (a) => !defaultBranch?.value || a.branchId === defaultBranch.value
    );
    setFormAuditorium(
      branchAuds.length > 0
        ? { value: branchAuds[0].auditoriumId || branchAuds[0].id, label: branchAuds[0].name }
        : auditoriumOptions[0] || null
    );
    setFormStartTime("");
    setFormBasePrice("10.00");
    setFormError("");
    setAddModalOpen(true);
  };

  const handleSaveAdd = async () => {
    if (!formMovie || !formAuditorium || !formStartTime || !formBasePrice) {
      setFormError("Please fill out all fields.");
      return;
    }
    try {
      await apiClient("/api/v1/admin/showtimes", {
        method: "POST",
        body: JSON.stringify({
          movieId: formMovie.value,
          auditoriumId: formAuditorium.value,
          startsAt: new Date(formStartTime).toISOString(),
          basePrice: Number(formBasePrice)
        })
      });
      setAddModalOpen(false);
      refetch();
      setNotifications([{ type: "success", content: "Showtime added successfully.", dismissible: true }]);
    } catch (err) {
      setFormError(err.message || "Failed to add showtime.");
    }
  };

  // --- Edit Modal ---
  const openEditModal = () => {
    if (!selectedItem) return;
    const movieVal = selectedItem.movieId || selectedItem.title || "";
    const movieLbl = selectedItem.movieTitle || selectedItem.title || "Unknown";
    setFormMovie({ value: movieVal, label: movieLbl });
    const audVal = selectedItem.auditoriumId || "";
    const audLbl = selectedItem.auditoriumName || selectedItem.auditorium || "Unknown";
    setFormAuditorium({ value: audVal, label: audLbl });
    setFormStartTime(selectedItem.startsAt || selectedItem.startTime ? new Date(selectedItem.startsAt || selectedItem.startTime).toISOString().slice(0, 16) : "");
    setFormBasePrice(String(selectedItem.basePrice || "10.00"));
    setFormError("");
    setEditModalOpen(true);
  };

  const handleSaveEdit = async () => {
    if (!selectedItem) return;
    try {
      await apiClient(`/api/v1/admin/showtimes/${selectedItem.showtimeId}`, {
        method: "PUT",
        body: JSON.stringify({
          startsAt: new Date(formStartTime).toISOString(),
          basePrice: Number(formBasePrice)
        })
      });
      setEditModalOpen(false);
      refetch();
      setNotifications([{ type: "success", content: "Showtime updated successfully.", dismissible: true }]);
    } catch (err) {
      setFormError(err.message || "Failed to update showtime.");
    }
  };

  // --- Delete ---
  const handleDelete = async () => {
    if (!selectedItem) return;
    try {
      await apiClient(`/api/v1/admin/showtimes/${selectedItem.showtimeId}`, {
        method: "DELETE"
      });
      setDeleteModalOpen(false);
      refetch();
      setNotifications([{ type: "success", content: "Showtime deleted.", dismissible: true }]);
    } catch (err) {
      setNotifications([{ type: "error", content: err.message || "Failed to delete showtime.", dismissible: true }]);
    }
  };

  // ─── CSV Import ───────────────────────────────────────────────────
  const openCsvModal = () => {
    setCsvFile(null);
    setCsvFileName("");
    setCsvValidationResults(null);
    setCsvValidating(false);
    setCsvCommitting(false);
    setCsvError("");
    setCsvModalOpen(true);
  };

  const handleCsvFileChange = (e) => {
    const file = e.target.files?.[0];
    if (file) {
      setCsvFile(file);
      setCsvFileName(file.name);
      setCsvValidationResults(null);
      setCsvError("");
    }
  };

  const handleCsvValidate = async () => {
    if (!csvFile) {
      setCsvError("Please select a CSV file first.");
      return;
    }
    setCsvValidating(true);
    setCsvError("");
    try {
      const text = await csvFile.text();
      const result = await apiClient("/api/v1/admin/showtimes/bulk-import?execute=false", {
        method: "POST",
        body: JSON.stringify({ csvContent: text, execute: false })
      });
      const rows = Array.isArray(result) ? result : Array.isArray(result.rows) ? result.rows : Array.isArray(result.results) ? result.results : [];
      setCsvValidationResults(rows);
    } catch (err) {
      setCsvError(err.message || "Validation failed.");
    } finally {
      setCsvValidating(false);
    }
  };

  const handleCsvCommit = async () => {
    if (!csvFile || !csvValidationResults) return;
    setCsvCommitting(true);
    setCsvError("");
    try {
      const text = await csvFile.text();
      await apiClient("/api/v1/admin/showtimes/bulk-import?execute=true", {
        method: "POST",
        body: JSON.stringify({ csvContent: text, execute: true })
      });
      setCsvModalOpen(false);
      refetch();
      setNotifications([
        {
          type: "success",
          content: "Showtimes bulk imported successfully.",
          dismissible: true,
          id: "import-success-" + Date.now(),
          onDismiss: () => setNotifications([]),
        },
      ]);
    } catch (err) {
      setCsvError(err.message || "Commit failed.");
    } finally {
      setCsvCommitting(false);
    }
  };

  const csvHasValidRows =
    csvValidationResults &&
    csvValidationResults.some(
      (r) => (r.status || "").toLowerCase() === "valid"
    );

  // ─── Render ───────────────────────────────────────────────────────
  return (
    <ContentLayout
      header={
        <Header
          variant="h1"
          description="Schedule showtimes, manage auditorium assignments, and bulk-import via CSV."
          info={
            <StatusIndicator
              type={error ? "error" : isFetching ? "loading" : "success"}
            >
              {error
                ? "Sync Error"
                : isFetching
                ? "Syncing..."
                : "Live Showtime Sync"}
            </StatusIndicator>
          }
          actions={
            <SpaceBetween direction="horizontal" size="xs">
              <Button
                iconName="refresh"
                onClick={() => {
                  setLocalShowtimes(null);
                  refetch();
                }}
                loading={isFetching}
              >
                Refresh
              </Button>
              <Button
                disabled={selectedCount === 0}
                onClick={() => setDeleteModalOpen(true)}
              >
                Delete
              </Button>
              <Button disabled={selectedCount === 0} onClick={openEditModal}>
                Edit
              </Button>
              <Button onClick={openCsvModal}>
                Bulk CSV Import
              </Button>
              <Button variant="primary" iconName="add-plus" onClick={openAddModal}>
                Add Showtime
              </Button>
            </SpaceBetween>
          }
        >
          Showtime Schedule
        </Header>
      }
    >
      <SpaceBetween size="l">
        {notifications.length > 0 && <Flashbar items={notifications} />}

        {error && (
          <Alert type="error" header="Failed to load showtimes">
            {error.message}
          </Alert>
        )}

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
                {filteredShowtimesByBranch.length}{" "}
                {filteredShowtimesByBranch.length === 1 ? "Showtime" : "Showtimes"}
              </Badge>
            )}
            {selectedBranchOption?.value !== "all" && (
              <Button variant="link" onClick={() => handleBranchChange(ALL_BRANCHES_OPTION)}>
                Show all branches
              </Button>
            )}
          </div>
        </Container>

        <Table
          {...collectionProps}
          selectionType="single"
          columnDefinitions={[
            {
              id: "movieTitle",
              header: "Movie Title",
              sortingField: "movieTitle",
              cell: (item) => (
                <span style={{ fontWeight: 600 }}>
                  {item.movieTitle || item.title || movieMap[item.movieId] || "-"}
                </span>
              ),
              isRowHeader: true,
            },
            ...(!selectedBranchOption || selectedBranchOption.value === "all"
              ? [
                  {
                    id: "branch",
                    header: "Cinema Branch",
                    sortingField: "branchId",
                    cell: (item) => (
                      <Badge color="blue">
                        {branchMap[item.branchId] || item.branchName || "Cinema Branch"}
                      </Badge>
                    ),
                  },
                ]
              : []),
            {
              id: "auditorium",
              header: "Auditorium / Hall",
              sortingField: "auditoriumName",
              cell: (item) => (
                <span style={{ fontWeight: 500 }}>
                  {item.auditoriumName || item.auditorium || "-"}
                </span>
              ),
            },
            {
              id: "startTime",
              header: "Start Time",
              sortingField: "startsAt",
              cell: (item) => formatDateTime(item.startsAt || item.startTime),
            },
            {
              id: "endTime",
              header: "End Time",
              sortingField: "endsAt",
              cell: (item) => formatDateTime(item.endsAt || item.endTime),
            },
            {
              id: "basePrice",
              header: "Base Price",
              sortingField: "basePrice",
              cell: (item) => formatPrice(item.basePrice),
            },
            {
              id: "status",
              header: "Status",
              sortingField: "status",
              cell: (item) => renderShowtimeStatus(item.status),
            },
            {
              id: "cleaningBuffer",
              header: "Cleaning Buffer",
              cell: (item) =>
                item.cleaningBufferMinutes != null
                  ? `${item.cleaningBufferMinutes} min`
                  : "15 min",
            },
          ]}
          items={items}
          loading={isLoading}
          loadingText="Loading showtimes..."
          filter={
            <TextFilter
              {...filterProps}
              filteringPlaceholder="Filter by movie, auditorium, status..."
              countText={`${filteredItemsCount} ${
                filteredItemsCount === 1 ? "match" : "matches"
              }`}
            />
          }
          header={
            <Header
              counter={filteredShowtimesByBranch ? `(${filteredShowtimesByBranch.length})` : undefined}
            >
              Showtimes
            </Header>
          }
          pagination={<Pagination {...paginationProps} />}
          empty={
            <Box textAlign="center" color="inherit">
              <b>No showtimes found</b>
              <Box padding={{ bottom: "s" }} variant="p" color="inherit">
                No showtimes currently scheduled. Click "+ Add Showtime" to create one.
              </Box>
            </Box>
          }
        />

        {/* ─── Add Showtime Modal ─────────────────────────────────── */}
        <Modal
          visible={addModalOpen}
          onDismiss={() => setAddModalOpen(false)}
          header="Add New Showtime"
          closeAriaLabel="Close modal"
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button variant="link" onClick={() => setAddModalOpen(false)}>
                  Cancel
                </Button>
                <Button variant="primary" onClick={handleSaveAdd}>
                  Add Showtime
                </Button>
              </SpaceBetween>
            </Box>
          }
        >
          <SpaceBetween size="m">
            {formError && (
              <Alert type="error" dismissible onDismiss={() => setFormError("")}>
                {formError}
              </Alert>
            )}
            <FormField label="Cinema Branch" description="Select cinema location">
              <Select
                selectedOption={formBranch}
                onChange={({ detail }) => {
                  setFormBranch(detail.selectedOption);
                  const branchAuds = auditoriums.filter(
                    (a) =>
                      !detail.selectedOption?.value ||
                      detail.selectedOption.value === "all" ||
                      a.branchId === detail.selectedOption.value
                  );
                  setFormAuditorium(
                    branchAuds.length > 0
                      ? { value: branchAuds[0].auditoriumId || branchAuds[0].id, label: branchAuds[0].name }
                      : null
                  );
                }}
                options={branchOptions.filter((b) => b.value !== "all")}
                placeholder="Select cinema branch..."
              />
            </FormField>
            <FormField label="Movie">
              <Select
                selectedOption={formMovie}
                onChange={({ detail }) => setFormMovie(detail.selectedOption)}
                options={movieOptions}
                placeholder="Select a movie"
                filteringType="auto"
              />
            </FormField>
            <FormField label="Auditorium / Hall">
              <Select
                selectedOption={formAuditorium}
                onChange={({ detail }) =>
                  setFormAuditorium(detail.selectedOption)
                }
                options={formAuditoriumOptions}
                placeholder="Select an auditorium"
              />
            </FormField>
            <ColumnLayout columns={2}>
              <FormField
                label="Start Time"
                description="Select date and time for the showtime."
              >
                <Input
                  type="datetime-local"
                  value={formStartTime}
                  onChange={({ detail }) => setFormStartTime(detail.value)}
                />
              </FormField>
              <FormField label="Base Price ($)">
                <Input
                  type="number"
                  value={formBasePrice}
                  onChange={({ detail }) => setFormBasePrice(detail.value)}
                  placeholder="10.00"
                />
              </FormField>
            </ColumnLayout>
          </SpaceBetween>
        </Modal>

        {/* ─── Edit Showtime Modal ────────────────────────────────── */}
        <Modal
          visible={editModalOpen}
          onDismiss={() => setEditModalOpen(false)}
          header={`Edit Showtime: ${
            selectedItem?.movieTitle || selectedItem?.title || ""
          }`}
          closeAriaLabel="Close modal"
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button variant="link" onClick={() => setEditModalOpen(false)}>
                  Cancel
                </Button>
                <Button variant="primary" onClick={handleSaveEdit}>
                  Save Changes
                </Button>
              </SpaceBetween>
            </Box>
          }
        >
          <SpaceBetween size="m">
            {formError && (
              <Alert type="error" dismissible onDismiss={() => setFormError("")}>
                {formError}
              </Alert>
            )}
            <FormField label="Movie">
              <Select
                selectedOption={formMovie}
                onChange={({ detail }) => setFormMovie(detail.selectedOption)}
                options={movieOptions}
                placeholder="Select a movie"
                filteringType="auto"
              />
            </FormField>
            <FormField label="Auditorium">
              <Select
                selectedOption={formAuditorium}
                onChange={({ detail }) =>
                  setFormAuditorium(detail.selectedOption)
                }
                options={auditoriumOptions}
                placeholder="Select an auditorium"
              />
            </FormField>
            <ColumnLayout columns={2}>
              <FormField label="Start Time">
                <Input
                  type="datetime-local"
                  value={formStartTime}
                  onChange={({ detail }) => setFormStartTime(detail.value)}
                />
              </FormField>
              <FormField label="Base Price ($)">
                <Input
                  type="number"
                  value={formBasePrice}
                  onChange={({ detail }) => setFormBasePrice(detail.value)}
                />
              </FormField>
            </ColumnLayout>
          </SpaceBetween>
        </Modal>

        {/* ─── Delete Confirmation Modal ──────────────────────────── */}
        <Modal
          visible={deleteModalOpen}
          onDismiss={() => setDeleteModalOpen(false)}
          header="Delete Showtime"
          closeAriaLabel="Close modal"
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button
                  variant="link"
                  onClick={() => setDeleteModalOpen(false)}
                >
                  Cancel
                </Button>
                <Button variant="primary" onClick={handleDelete}>
                  Delete
                </Button>
              </SpaceBetween>
            </Box>
          }
        >
          <Box>
            Are you sure you want to delete the showtime for{" "}
            <b>{selectedItem?.movieTitle || selectedItem?.title || "this movie"}</b> at{" "}
            <b>{selectedItem?.auditoriumName || selectedItem?.auditorium || "the selected auditorium"}</b>?
            This action cannot be undone.
          </Box>
        </Modal>

        {/* ─── Bulk CSV Import Modal ──────────────────────────────── */}
        <Modal
          visible={csvModalOpen}
          onDismiss={() => setCsvModalOpen(false)}
          header="Bulk CSV Import"
          closeAriaLabel="Close modal"
          size="large"
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button variant="link" onClick={() => setCsvModalOpen(false)}>
                  Cancel
                </Button>
                <Button
                  onClick={handleCsvValidate}
                  loading={csvValidating}
                  disabled={!csvFile}
                >
                  Validate
                </Button>
                <Button
                  variant="primary"
                  onClick={handleCsvCommit}
                  loading={csvCommitting}
                  disabled={!csvHasValidRows}
                >
                  Commit Import
                </Button>
              </SpaceBetween>
            </Box>
          }
        >
          <SpaceBetween size="m">
            <FormField
              label="CSV File"
              description="Upload a CSV with columns: movie_title, auditorium, start_time, base_price"
            >
              <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
                <label
                  htmlFor="csv-upload"
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "6px",
                    padding: "6px 16px",
                    borderRadius: "8px",
                    border: "1px solid #545b64",
                    background: "#fafafa",
                    cursor: "pointer",
                    fontSize: "14px",
                    fontWeight: 500,
                  }}
                >
                  Choose File
                </label>
                <input
                  id="csv-upload"
                  type="file"
                  accept=".csv"
                  onChange={handleCsvFileChange}
                  style={{ display: "none" }}
                />
                <span style={{ fontSize: "13px", color: "#545b64" }}>
                  {csvFileName || "No file selected"}
                </span>
              </div>
            </FormField>

            {csvError && (
              <Alert type="error" dismissible onDismiss={() => setCsvError("")}>
                {csvError}
              </Alert>
            )}

            {csvValidationResults && (
              <SpaceBetween size="s">
                <Header variant="h3">
                  Validation Results ({csvValidationResults.length} rows)
                </Header>
                <Table
                  columnDefinitions={[
                    {
                      id: "row",
                      header: "Row #",
                      cell: (item) => item.row ?? item.rowNumber ?? "-",
                    },
                    {
                      id: "movieTitle",
                      header: "Movie Title",
                      cell: (item) =>
                        item.movieTitle || item.movie_title || "-",
                    },
                    {
                      id: "auditorium",
                      header: "Auditorium",
                      cell: (item) => item.auditorium || "-",
                    },
                    {
                      id: "startTime",
                      header: "Start Time",
                      cell: (item) =>
                        item.startTime || item.start_time || "-",
                    },
                    {
                      id: "basePrice",
                      header: "Base Price",
                      cell: (item) =>
                        item.basePrice != null
                          ? formatPrice(item.basePrice)
                          : item.base_price != null
                          ? formatPrice(item.base_price)
                          : "-",
                    },
                    {
                      id: "status",
                      header: "Status",
                      cell: (item) => renderCsvRowStatus(item.status),
                    },
                    {
                      id: "message",
                      header: "Message",
                      cell: (item) => item.message || item.error || "-",
                    },
                  ]}
                  items={csvValidationResults}
                  empty={
                    <Box textAlign="center" color="inherit">
                      <b>No validation results</b>
                    </Box>
                  }
                />
              </SpaceBetween>
            )}
          </SpaceBetween>
        </Modal>
      </SpaceBetween>
    </ContentLayout>
  );
}

export default function ShowtimesContent() {
  return (
    <QueryWrapper>
      <ShowtimesContentInner />
    </QueryWrapper>
  );
}




