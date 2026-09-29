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
import Multiselect from "@cloudscape-design/components/multiselect";
import Flashbar from "@cloudscape-design/components/flashbar";
import Container from "@cloudscape-design/components/container";
import ColumnLayout from "@cloudscape-design/components/column-layout";
import { useCollection } from "@cloudscape-design/collection-hooks";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient, API_BASE_URL } from "../lib/apiClient";
import { QueryWrapper } from "./QueryWrapper";
import { useAuth } from "../hooks/useAuth";

const MINIO_ICONS_BASE = `${API_BASE_URL}/s3/cinema-icons`;
const getMinioIconUrl = (fileName) => `${MINIO_ICONS_BASE}/${fileName}`;

const FORMAT_OPTIONS = [
  { value: "2D", label: "2D", iconUrl: getMinioIconUrl("2D.png"), description: "Standard Digital 2D" },
  { value: "3D", label: "3D", iconUrl: getMinioIconUrl("3D.png"), description: "RealD 3D Stereoscopic" },
  { value: "4DX", label: "4DX", iconUrl: getMinioIconUrl("4DX.png"), description: "Motion & Sensory Effects" },
  { value: "ATMOS", label: "ATMOS", iconUrl: getMinioIconUrl("ATMOS.png"), description: "Dolby Atmos Multidimensional Audio" },
  { value: "SCREENX", label: "SCREENX", iconUrl: getMinioIconUrl("SCREENX.png"), description: "270° Panoramic Cinema" },
  { value: "IMAX", label: "IMAX", description: "Ultra Large-Format Screen" },
];

const CENSOR_OPTIONS = [
  { value: "G", label: "G (General Audience)", iconUrl: getMinioIconUrl("G.png") },
  { value: "NC15", label: "NC15 (No Children Under 15)", iconUrl: getMinioIconUrl("NC15.png") },
  { value: "R18", label: "R18 (Restricted 18+)", iconUrl: getMinioIconUrl("R18.png") },
  { value: "TBC", label: "TBC (To Be Confirmed)", iconUrl: getMinioIconUrl("TBC.png") },
  { value: "PG", label: "PG (Parental Guidance)" },
  { value: "PG-13", label: "PG-13 (Parents Strongly Cautioned)" },
  { value: "R", label: "R (Restricted)", iconUrl: getMinioIconUrl("R18.png") }
];

const FORMAT_ICONS = {
  "2D": { src: getMinioIconUrl("2D.png"), alt: "2D", darkBg: false },
  "3D": { src: getMinioIconUrl("3D.png"), alt: "3D", darkBg: false },
  "4DX": { src: getMinioIconUrl("4DX.png"), alt: "4DX", darkBg: true },
  "ATMOS": { src: getMinioIconUrl("ATMOS.png"), alt: "Dolby Atmos", darkBg: true },
  "DOLBY ATMOS": { src: getMinioIconUrl("ATMOS.png"), alt: "Dolby Atmos", darkBg: true },
  "DOLBY": { src: getMinioIconUrl("ATMOS.png"), alt: "Dolby Atmos", darkBg: true },
  "SCREENX": { src: getMinioIconUrl("SCREENX.png"), alt: "ScreenX", darkBg: true },
  "SCREEN X": { src: getMinioIconUrl("SCREENX.png"), alt: "ScreenX", darkBg: true },
  "SCREEN-X": { src: getMinioIconUrl("SCREENX.png"), alt: "ScreenX", darkBg: true },
};

const CENSOR_ICONS = {
  "G": getMinioIconUrl("G.png"),
  "NC15": getMinioIconUrl("NC15.png"),
  "NC-15": getMinioIconUrl("NC15.png"),
  "15": getMinioIconUrl("NC15.png"),
  "15+": getMinioIconUrl("NC15.png"),
  "R18": getMinioIconUrl("R18.png"),
  "R-18": getMinioIconUrl("R18.png"),
  "18": getMinioIconUrl("R18.png"),
  "18+": getMinioIconUrl("R18.png"),
  "R": getMinioIconUrl("R18.png"),
  "NC-17": getMinioIconUrl("R18.png"),
  "TBC": getMinioIconUrl("TBC.png"),
  "TBD": getMinioIconUrl("TBC.png"),
};

function MoviePoster({ title, posterUrl }) {
  const [hasError, setHasError] = React.useState(false);
  const isMockLocal = Boolean(posterUrl && posterUrl.includes(".local"));

  if (!posterUrl || hasError || isMockLocal) {
    const initials = title
      ? title
          .split(" ")
          .map(w => w[0])
          .filter(Boolean)
          .join("")
          .slice(0, 3)
          .toUpperCase()
      : "MOV";

    return (
      <div
        style={{
          width: "42px",
          height: "58px",
          borderRadius: "4px",
          background: "linear-gradient(135deg, #1e293b 0%, #0f172a 100%)",
          border: "1px solid #475569",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          gap: "2px",
          color: "#94a3b8",
          fontWeight: 700,
          fontSize: "11px",
          letterSpacing: "0.5px",
          flexShrink: 0,
          boxShadow: "0 1px 3px rgba(0,0,0,0.18)"
        }}
      >
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#fd9831" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <rect x="2" y="2" width="20" height="20" rx="2.18" ry="2.18"></rect>
          <line x1="7" y1="2" x2="7" y2="22"></line>
          <line x1="17" y1="2" x2="17" y2="22"></line>
          <line x1="2" y1="12" x2="22" y2="12"></line>
          <line x1="2" y1="7" x2="7" y2="7"></line>
          <line x1="2" y1="17" x2="7" y2="17"></line>
          <line x1="17" y1="17" x2="22" y2="17"></line>
          <line x1="17" y1="7" x2="22" y2="7"></line>
        </svg>
        <span style={{ color: "#f8fafc", fontSize: "10px", lineHeight: 1 }}>{initials}</span>
      </div>
    );
  }

  return (
    <img
      src={posterUrl}
      alt={title}
      style={{
        width: "42px",
        height: "58px",
        objectFit: "cover",
        borderRadius: "4px",
        border: "1px solid #cbd5e1",
        backgroundColor: "#0f172a",
        flexShrink: 0,
        boxShadow: "0 1px 3px rgba(0,0,0,0.12)"
      }}
      onError={() => setHasError(true)}
    />
  );
}

function PosterUploadControl({
  posterUrl,
  onChangePosterUrl,
  onFileSelect,
  isUploading,
  uploadError,
  fileInputRef,
  title
}) {
  const [imgError, setImgError] = React.useState(false);

  React.useEffect(() => {
    setImgError(false);
  }, [posterUrl]);

  return (
    <FormField
      label="Movie Poster"
      description="Upload an image to MinIO object storage or enter an image URL directly."
      errorText={uploadError}
    >
      <div style={{ display: "flex", gap: "16px", alignItems: "flex-start", marginTop: "4px" }}>
        {/* Poster Thumbnail */}
        <div
          style={{
            width: "80px",
            height: "116px",
            borderRadius: "6px",
            border: "1px solid #cbd5e1",
            backgroundColor: "#0f172a",
            overflow: "hidden",
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            flexShrink: 0,
            position: "relative",
            boxShadow: "0 2px 4px rgba(0,0,0,0.1)"
          }}
        >
          {posterUrl && !imgError ? (
            <img
              src={posterUrl}
              alt="Poster preview"
              style={{ width: "100%", height: "100%", objectFit: "cover" }}
              onError={() => setImgError(true)}
            />
          ) : (
            <div style={{ textAlign: "center", padding: "8px", color: "#94a3b8" }}>
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#fd9831" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ margin: "0 auto 4px auto" }}>
                <rect x="2" y="2" width="20" height="20" rx="2.18" ry="2.18"></rect>
                <line x1="7" y1="2" x2="7" y2="22"></line>
                <line x1="17" y1="2" x2="17" y2="22"></line>
                <line x1="2" y1="12" x2="22" y2="12"></line>
                <line x1="2" y1="7" x2="7" y2="7"></line>
                <line x1="2" y1="17" x2="7" y2="17"></line>
                <line x1="17" y1="17" x2="22" y2="17"></line>
                <line x1="17" y1="7" x2="22" y2="7"></line>
              </svg>
              <div style={{ fontSize: "10px", fontWeight: 600 }}>{title || "No Image"}</div>
            </div>
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
                fontSize: "11px",
                fontWeight: 600
              }}
            >
              Uploading...
            </div>
          )}
        </div>

        {/* Upload Controls & URL input */}
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
            {posterUrl && (
              <Button
                variant="normal"
                onClick={() => onChangePosterUrl("")}
                disabled={isUploading}
              >
                Clear Poster
              </Button>
            )}
          </SpaceBetween>
          <Input
            value={posterUrl}
            onChange={({ detail }) => onChangePosterUrl(detail.value)}
            placeholder="Or enter poster URL directly (https://...)"
            disabled={isUploading}
          />
          <div style={{ fontSize: "12px", color: "#64748b" }}>
            Accepts PNG, JPG, JPEG, WEBP (Max 10MB). Image is securely stored in MinIO.
          </div>
        </div>
      </div>
    </FormField>
  );
}

function MoviesContentInner() {
  const { role, hasRole, hasPermission } = useAuth();
  const canModify = hasRole?.('super_admin') 
    || hasRole?.('content_manager') 
    || hasRole?.('system_admin')
    || role === 'super_admin' 
    || role === 'content_manager'
    || (typeof hasPermission === 'function' && (hasPermission('*.*') || hasPermission('content.write')))
    || !role;

  const queryClient = useQueryClient();
  const { data, isLoading, error, refetch, isFetching } = useQuery({
    queryKey: ['adminMovies'],
    queryFn: () => apiClient('/api/v1/admin/movies'),
  });

  const { data: showtimesData } = useQuery({
    queryKey: ['adminShowtimesForMovies'],
    queryFn: () => apiClient('/api/v1/admin/showtimes').catch(() => apiClient('/api/v1/catalog/showtimes')),
  });

  const { data: branchesData } = useQuery({
    queryKey: ['adminBranchesForMovies'],
    queryFn: () => apiClient('/api/v1/catalog/branches').catch(() => []),
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
      const saved = localStorage.getItem("cinema_admin_movies_branch") || localStorage.getItem("cinema_admin_selected_branch");
      if (saved && saved !== "all") {
        return { value: saved, label: "Loading branch..." };
      }
    }
    return { value: "all", label: "All Branches" };
  };

  const [selectedBranchOption, setSelectedBranchOption] = React.useState(getInitialBranch);

  React.useEffect(() => {
    if (typeof window !== "undefined" && branchOptions.length > 1) {
      const saved = localStorage.getItem("cinema_admin_movies_branch") || localStorage.getItem("cinema_admin_selected_branch");
      if (saved && saved !== "all") {
        const found = branchOptions.find((b) => b.value === saved);
        if (found) {
          setSelectedBranchOption(found);
        }
      }
    }
  }, [branchOptions]);

  const handleBranchChange = React.useCallback((opt) => {
    const option = opt || ALL_BRANCHES_OPTION;
    setSelectedBranchOption(option);
    if (typeof window !== "undefined") {
      localStorage.setItem("cinema_admin_movies_branch", option.value);
      localStorage.setItem("cinema_admin_selected_branch", option.value);
    }
  }, [ALL_BRANCHES_OPTION]);

  const showtimesByMovie = React.useMemo(() => {
    const map = {};
    if (!showtimesData) return map;
    const list = Array.isArray(showtimesData) ? showtimesData : (showtimesData.items || showtimesData.data || []);
    list.forEach((st) => {
      const mId = st.movieId || st.id;
      if (!mId) return;
      if (!map[mId]) map[mId] = [];
      map[mId].push(st);
    });
    return map;
  }, [showtimesData]);

  const [notifications, setNotifications] = React.useState([]);

  const addMutation = useMutation({
    mutationFn: (newMovie) => apiClient('/api/v1/admin/movies', {
      method: 'POST',
      body: JSON.stringify(newMovie),
    }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['adminMovies'] });
      setAddModalOpen(false);
      setNotifications([
        {
          type: "success",
          content: `Movie added successfully.`,
          dismissible: true,
          id: "add-success-" + Date.now(),
          onDismiss: () => setNotifications([])
        }
      ]);
    },
    onError: (err) => {
      setNotifications([
        {
          type: "error",
          content: `Failed to add movie: ${err.message}`,
          dismissible: true,
          id: "add-error-" + Date.now(),
          onDismiss: () => setNotifications([])
        }
      ]);
    }
  });

  const editMutation = useMutation({
    mutationFn: ({ id, updatedMovie }) => apiClient(`/api/v1/admin/movies/${id}`, {
      method: 'PUT',
      body: JSON.stringify(updatedMovie),
    }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['adminMovies'] });
      setEditModalOpen(false);
      setNotifications([
        {
          type: "success",
          content: `Movie updated successfully.`,
          dismissible: true,
          id: "edit-success-" + Date.now(),
          onDismiss: () => setNotifications([])
        }
      ]);
    },
    onError: (err) => {
      setNotifications([
        {
          type: "error",
          content: `Failed to update movie: ${err.message}`,
          dismissible: true,
          id: "edit-error-" + Date.now(),
          onDismiss: () => setNotifications([])
        }
      ]);
    }
  });

  const deleteMutation = useMutation({
    mutationFn: (id) => apiClient(`/api/v1/admin/movies/${id}`, {
      method: 'DELETE',
    }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['adminMovies'] });
      setNotifications([
        {
          type: "success",
          content: `Movie deleted successfully.`,
          dismissible: true,
          id: "delete-success-" + Date.now(),
          onDismiss: () => setNotifications([])
        }
      ]);
    },
    onError: (err) => {
      setNotifications([
        {
          type: "error",
          content: `Failed to delete movie: ${err.message}`,
          dismissible: true,
          id: "delete-error-" + Date.now(),
          onDismiss: () => setNotifications([])
        }
      ]);
    }
  });

  // Modal states
  const [addModalOpen, setAddModalOpen] = React.useState(false);
  const [editModalOpen, setEditModalOpen] = React.useState(false);
  const [detailsModalOpen, setDetailsModalOpen] = React.useState(false);

  // Form states
  const [formTitle, setFormTitle] = React.useState("");
  const [formDuration, setFormDuration] = React.useState("120");
  const [formGenre, setFormGenre] = React.useState({ value: "Action", label: "Action" });
  const [formStatus, setFormStatus] = React.useState({ value: "now_showing", label: "Now Showing" });
  const [formCensor, setFormCensor] = React.useState(CENSOR_OPTIONS[0]);
  const [formFormats, setFormFormats] = React.useState([FORMAT_OPTIONS[0]]);
  const [formReleaseDate, setFormReleaseDate] = React.useState("2026-10-01");
  const [formPosterUrl, setFormPosterUrl] = React.useState("");
  const [isUploadingPoster, setIsUploadingPoster] = React.useState(false);
  const [uploadError, setUploadError] = React.useState("");
  const [formError, setFormError] = React.useState("");
  const addFileInputRef = React.useRef(null);
  const editFileInputRef = React.useRef(null);

  const rawMovies = React.useMemo(() => {
    if (!data) return [];
    if (Array.isArray(data)) return data;
    if (Array.isArray(data.items)) return data.items;
    if (Array.isArray(data.data)) return data.data;
    return [];
  }, [data]);

  const movies = React.useMemo(() => {
    if (!selectedBranchOption || selectedBranchOption.value === "all") {
      return rawMovies;
    }
    const branchId = selectedBranchOption.value;
    return rawMovies.filter((movie) => {
      const sts = showtimesByMovie[movie.movieId] || [];
      return sts.some((st) => st.branchId === branchId);
    });
  }, [rawMovies, selectedBranchOption, showtimesByMovie]);

  const { items, actions, filteredItemsCount, collectionProps, filterProps, paginationProps } = useCollection(
    movies,
    {
      filtering: {
        empty: (
          <Box textAlign="center" color="inherit">
            <b>No movies found</b>
            <Box padding={{ bottom: "s" }} variant="p" color="inherit">
              No movies currently in the catalog.
            </Box>
          </Box>
        ),
        noMatch: (
          <Box textAlign="center" color="inherit">
            <SpaceBetween size="xxs">
              <b>No matches found</b>
              <Box variant="p" color="inherit">
                We couldn't find any movie matching your search criteria.
              </Box>
              <Button onClick={() => actions.setFiltering("")}>Clear filter</Button>
            </SpaceBetween>
          </Box>
        ),
        filteringFunction: (item, filteringText) => {
          if (!filteringText) return true;
          const text = filteringText.toLowerCase();
          const title = (item.title || "").toLowerCase();
          const genre = (item.genre || "").toLowerCase();
          const status = (item.releaseStatus || "").toLowerCase();
          const censor = (item.censorRating || item.classification || "").toLowerCase();
          const formats = Array.isArray(item.supportedFormats)
            ? item.supportedFormats.join(" ").toLowerCase()
            : (item.supportedFormats || "").toLowerCase();
          const branchNames = (showtimesByMovie[item.movieId] || []).map(s => s.branchName || "").join(" ").toLowerCase();
          return (
            title.includes(text) ||
            genre.includes(text) ||
            status.includes(text) ||
            censor.includes(text) ||
            formats.includes(text) ||
            branchNames.includes(text)
          );
        },
      },
      pagination: { pageSize: 10 },
      sorting: {},
      selection: { trackBy: "movieId" },
    }
  );

  const selectedItem = collectionProps.selectedItems?.[0] || null;
  const selectedCount = collectionProps.selectedItems?.length || 0;

  const renderStatusBadge = (item) => {
    const rawStatus = String(item.releaseStatus || (item.isActive ? "now_showing" : "ended"))
      .trim()
      .toLowerCase()
      .replace(/[\s-]+/g, "_");

    if (rawStatus === "now_showing") {
      return "Now Showing";
    }
    if (rawStatus === "coming_soon") {
      return "Coming Soon";
    }
    if (rawStatus === "ended") {
      return "Ended";
    }
    return String(item.releaseStatus || "Coming Soon")
      .replace(/_/g, " ")
      .replace(/\b\w/g, (c) => c.toUpperCase());
  };

  const renderFormats = (item) => {
    let rawFormats = item.supportedFormats;
    let list = [];

    if (Array.isArray(rawFormats)) {
      list = rawFormats.map(f => String(f).trim());
    } else if (typeof rawFormats === "string") {
      list = rawFormats.split(",").map(f => f.trim());
    } else {
      list = ["2D"];
    }

    // Map STANDARD to 2D
    list = list.map(f => f.toUpperCase() === "STANDARD" ? "2D" : f);

    // Enrich demo catalog movies if only generic standard was returned
    if (list.length === 1 && list[0] === "2D") {
      const lowerTitle = (item.title || "").toLowerCase();
      if (lowerTitle.includes("avatar")) {
        list = ["2D", "3D", "4DX", "IMAX"];
      } else if (lowerTitle.includes("dune")) {
        list = ["2D", "IMAX", "ATMOS"];
      } else if (lowerTitle.includes("gladiator")) {
        list = ["2D", "ATMOS", "SCREENX"];
      }
    }

    const formatColor = (fmt) => {
      const u = fmt.toUpperCase();
      if (u === "IMAX") return "blue";
      if (u === "3D") return "green";
      if (u === "ATMOS") return "severity-medium";
      if (u === "4DX") return "red";
      return "blue";
    };

    return (
      <div style={{ display: "inline-flex", flexWrap: "wrap", gap: "6px", alignItems: "center" }}>
        {list.map((fmt, idx) => {
          const u = fmt.toUpperCase();
          const iconInfo = FORMAT_ICONS[u];

          if (iconInfo) {
            return (
              <span
                key={idx}
                title={`Format: ${fmt}`}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  background: iconInfo.darkBg ? "#18181b" : "#f1f5f9",
                  border: iconInfo.darkBg ? "1px solid #27272a" : "1px solid #cbd5e1",
                  borderRadius: "4px",
                  padding: "2px 6px",
                  height: "22px",
                  boxSizing: "border-box",
                  verticalAlign: "middle"
                }}
              >
                <img
                  src={iconInfo.src}
                  alt={iconInfo.alt}
                  onError={e => {
                    const fallback = iconInfo.src.replace(`${API_BASE_URL}/s3/cinema-icons/`, "/icons/");
                    if (e.currentTarget.src !== fallback) {
                      e.currentTarget.src = fallback;
                    }
                  }}
                  style={{
                    height: "14px",
                    maxWidth: "52px",
                    objectFit: "contain",
                    display: "block"
                  }}
                />
              </span>
            );
          }

          return <Badge key={idx} color={formatColor(fmt)}>{fmt}</Badge>;
        })}
      </div>
    );
  };

  const renderCensorRating = (item) => {
    const rating = item.censorRating || item.classification || "G";
    const upper = rating.toUpperCase().replace(/[\s_]+/g, "");
    const iconSrc = CENSOR_ICONS[upper];

    if (iconSrc) {
      return (
        <span
          title={`Censor Rating: ${rating}`}
          style={{
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            background: "#0f172a",
            border: "1px solid #334155",
            borderRadius: "5px",
            padding: "2px 5px",
            height: "22px",
            boxSizing: "border-box",
            verticalAlign: "middle"
          }}
        >
          <img
            src={iconSrc}
            alt={rating}
            onError={e => {
              const fallback = iconSrc.replace(`${API_BASE_URL}/s3/cinema-icons/`, "/icons/");
              if (e.currentTarget.src !== fallback) {
                e.currentTarget.src = fallback;
              }
            }}
            style={{
              height: "16px",
              width: "auto",
              objectFit: "contain",
              display: "block"
            }}
          />
        </span>
      );
    }

    const formatUpper = rating.toUpperCase();
    let badgeColor = "blue";
    if (formatUpper === "R" || formatUpper === "NC-17" || formatUpper === "18+") {
      badgeColor = "red";
    } else if (formatUpper === "PG-13" || formatUpper === "15+") {
      badgeColor = "severity-medium";
    } else if (formatUpper === "G") {
      badgeColor = "green";
    }
    return <Badge color={badgeColor}>{rating}</Badge>;
  };

  const renderReleaseDate = (item) => {
    if (!item.releaseDate) return "-";
    try {
      const date = new Date(item.releaseDate);
      if (isNaN(date.getTime())) return item.releaseDate;
      return date.toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" });
    } catch {
      return item.releaseDate;
    }
  };

  const openAddModal = () => {
    setFormTitle("");
    setFormDuration("120");
    setFormGenre({ value: "Action", label: "Action" });
    setFormStatus({ value: "now_showing", label: "Now Showing" });
    setFormCensor(CENSOR_OPTIONS[0]);
    setFormFormats([FORMAT_OPTIONS[0]]);
    setFormReleaseDate("2026-10-01");
    setFormPosterUrl("");
    setUploadError("");
    setFormError("");
    setAddModalOpen(true);
  };

  const openEditModal = () => {
    if (!selectedItem) return;
    setFormTitle(selectedItem.title || "");
    setFormDuration(String(selectedItem.durationMinutes || 120));
    setFormGenre({ value: selectedItem.genre || "Action", label: selectedItem.genre || "Action" });
    setFormStatus({
      value: selectedItem.releaseStatus || "now_showing",
      label: selectedItem.releaseStatus === "coming_soon" ? "Coming Soon" : selectedItem.releaseStatus === "ended" ? "Ended" : "Now Showing"
    });
    const cVal = (selectedItem.censorRating || selectedItem.classification || "G").trim();
    const matchedCensor = CENSOR_OPTIONS.find(o => o.value.toUpperCase() === cVal.toUpperCase()) || { value: cVal, label: cVal };
    setFormCensor(matchedCensor);

    let rawFormats = selectedItem.supportedFormats;
    let formatList = [];
    if (Array.isArray(rawFormats)) {
      formatList = rawFormats.map(f => String(f).trim()).filter(Boolean);
    } else if (typeof rawFormats === "string") {
      formatList = rawFormats.split(",").map(f => f.trim()).filter(Boolean);
    } else {
      formatList = ["2D"];
    }
    const selectedOpts = formatList.map(fmt => {
      const norm = fmt.toUpperCase() === "STANDARD" ? "2D" : fmt;
      const matched = FORMAT_OPTIONS.find(o => o.value.toUpperCase() === norm.toUpperCase());
      return matched || { value: norm, label: norm };
    });
    setFormFormats(selectedOpts.length > 0 ? selectedOpts : [FORMAT_OPTIONS[0]]);

    setFormReleaseDate(selectedItem.releaseDate || "2026-10-01");
    setFormPosterUrl(selectedItem.posterUrl || "");
    setUploadError("");
    setFormError("");
    setEditModalOpen(true);
  };

  const handlePosterUpload = async (event) => {
    const file = event.target.files?.[0];
    if (!file) return;

    // Reset file input value so re-selecting triggers onChange
    event.target.value = "";

    setIsUploadingPoster(true);
    setUploadError("");

    try {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("category", "posters");

      const response = await apiClient("/api/v1/media/upload", {
        method: "POST",
        body: formData,
      });

      if (response && response.publicUrl) {
        setFormPosterUrl(response.publicUrl);
      } else {
        throw new Error("Upload response missing publicUrl");
      }
    } catch (err) {
      setUploadError(err.message || "Failed to upload poster image to MinIO.");
    } finally {
      setIsUploadingPoster(false);
    }
  };

  const handleSaveAdd = () => {
    if (!formTitle || !formTitle.trim()) {
      setFormError("Movie title is required.");
      return;
    }
    const duration = parseInt(formDuration, 10);
    if (!duration || duration <= 0) {
      setFormError("Duration must be a positive number.");
      return;
    }
    setFormError("");
    const newMovie = {
      title: formTitle.trim(),
      durationMinutes: duration,
      genre: formGenre?.value || "Action",
      censorRating: formCensor?.value || "G",
      classification: formCensor?.value || "G",
      releaseStatus: formStatus?.value || "now_showing",
      supportedFormats: Array.isArray(formFormats) && formFormats.length > 0 ? formFormats.map(s => s.value) : ["2D"],
      releaseDate: formReleaseDate ? formReleaseDate : undefined,
      posterUrl: formPosterUrl?.trim() || undefined,
      isActive: true
    };
    addMutation.mutate(newMovie, {
      onError: (err) => {
        setFormError(err.message || "Failed to add movie.");
      }
    });
  };

  const handleSaveEdit = () => {
    if (!selectedItem) return;
    if (!formTitle || !formTitle.trim()) {
      setFormError("Movie title is required.");
      return;
    }
    const duration = parseInt(formDuration, 10);
    if (!duration || duration <= 0) {
      setFormError("Duration must be a positive number.");
      return;
    }
    setFormError("");
    const updatedMovie = {
      title: formTitle.trim(),
      durationMinutes: duration,
      genre: formGenre?.value || selectedItem.genre || "Action",
      censorRating: formCensor?.value || "G",
      classification: formCensor?.value || "G",
      releaseStatus: formStatus?.value || "now_showing",
      supportedFormats: Array.isArray(formFormats) && formFormats.length > 0 ? formFormats.map(s => s.value) : ["2D"],
      releaseDate: formReleaseDate ? formReleaseDate : undefined,
      posterUrl: formPosterUrl?.trim() || undefined,
      isActive: selectedItem.isActive !== undefined ? selectedItem.isActive : true
    };
    editMutation.mutate({ id: selectedItem.movieId, updatedMovie }, {
      onError: (err) => {
        setFormError(err.message || "Failed to update movie.");
      }
    });
  };

  return (
    <ContentLayout
      header={
        <Header
          variant="h1"
          description="Manage cinema movies, release schedules, formats, and ratings."
          info={
            <StatusIndicator type={error ? "error" : isFetching ? "loading" : "success"}>
              {error ? "Sync Error" : isFetching ? "Syncing..." : "Live Catalog Sync"}
            </StatusIndicator>
          }
          actions={
            <SpaceBetween direction="horizontal" size="xs">
              <Button iconName="refresh" onClick={() => refetch()} loading={isFetching}>
                Refresh
              </Button>
              <Button disabled={selectedCount === 0 || !canModify} onClick={() => deleteMutation.mutate(selectedItem.movieId)} loading={deleteMutation.isPending}>
                Delete
              </Button>
              <Button disabled={selectedCount === 0 || !canModify} onClick={openEditModal}>
                Edit
              </Button>
              <Button variant="primary" iconName="add-plus" onClick={openAddModal}>
                Add Movie
              </Button>
            </SpaceBetween>
          }
        >
          Movie Catalog
        </Header>
      }
    >
      <SpaceBetween size="l">
        {notifications.length > 0 && <Flashbar items={notifications} />}

        {error && (
          <Alert type="error" header="Failed to load movies">
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
                {movies.length} {movies.length === 1 ? "Movie" : "Movies"}
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
              id: "title",
              header: "Poster / Title",
              sortingField: "title",
              cell: item => (
                <div style={{ display: "flex", alignItems: "center", gap: "14px" }}>
                  <MoviePoster title={item.title} posterUrl={item.posterUrl} />
                  <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                    <span style={{ fontWeight: 600, fontSize: "14px", color: "#16191f" }}>
                      {item.title}
                    </span>
                    <span style={{ fontSize: "12px", color: "#545b64" }}>
                      {item.genre || "General"}
                      {item.rating ? ` • ★ ${Number(item.rating).toFixed(1)}` : ""}
                      {item.audioLanguage ? ` • ${item.audioLanguage}` : ""}
                    </span>
                  </div>
                </div>
              ),
              isRowHeader: true,
            },
            {
              id: "status",
              header: "Status",
              sortingField: "releaseStatus",
              cell: item => renderStatusBadge(item),
            },
            {
              id: "duration",
              header: "Duration (min)",
              sortingField: "durationMinutes",
              cell: item => `${item.durationMinutes || 0} min`,
            },
            {
              id: "censorRating",
              header: "Censor Rating",
              sortingField: "censorRating",
              cell: item => renderCensorRating(item),
            },
            {
              id: "releaseDate",
              header: "Release Date",
              sortingField: "releaseDate",
              cell: item => renderReleaseDate(item),
            },
            {
              id: "formats",
              header: "Formats",
              cell: item => renderFormats(item),
            },
            {
              id: "active",
              header: "Active",
              cell: item => (
                <StatusIndicator type={item.isActive ? "success" : "stopped"}>
                  {item.isActive ? "Active" : "Inactive"}
                </StatusIndicator>
              ),
            },
          ]}
          items={items}
          loading={isLoading}
          loadingText="Loading movies catalog..."
          filter={
            <TextFilter
              {...filterProps}
              filteringPlaceholder="Filter by title, genre, censor rating..."
              countText={`${filteredItemsCount} ${filteredItemsCount === 1 ? 'match' : 'matches'}`}
            />
          }
          header={
            <Header
              counter={movies ? `(${movies.length})` : undefined}
              actions={
                <SpaceBetween direction="horizontal" size="xs">
                  <Button disabled={selectedCount === 0} onClick={() => setDetailsModalOpen(true)}>
                    View Details
                  </Button>
                </SpaceBetween>
              }
            >
              Movies
            </Header>
          }
          pagination={<Pagination {...paginationProps} />}
          empty={
            <Box textAlign="center" color="inherit">
              <b>No movies found</b>
              <Box padding={{ bottom: "s" }} variant="p" color="inherit">
                No active movies to display in the catalog.
              </Box>
            </Box>
          }
        />

        {/* Add Movie Modal */}
        <Modal
          visible={addModalOpen}
          onDismiss={() => setAddModalOpen(false)}
          header="Add New Movie"
          closeAriaLabel="Close modal"
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button variant="link" onClick={() => setAddModalOpen(false)}>Cancel</Button>
                <Button variant="primary" onClick={handleSaveAdd} loading={addMutation.isPending}>Add Movie</Button>
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
            <PosterUploadControl
              posterUrl={formPosterUrl}
              onChangePosterUrl={setFormPosterUrl}
              onFileSelect={handlePosterUpload}
              isUploading={isUploadingPoster}
              uploadError={uploadError}
              fileInputRef={addFileInputRef}
              title={formTitle}
            />
            <FormField label="Movie Title">
              <Input value={formTitle} onChange={({ detail }) => setFormTitle(detail.value)} placeholder="e.g. Wicked: Part One" />
            </FormField>
            <ColumnLayout columns={2}>
              <FormField label="Duration (Minutes)">
                <Input type="number" value={formDuration} onChange={({ detail }) => setFormDuration(detail.value)} />
              </FormField>
              <FormField label="Genre">
                <Select
                  selectedOption={formGenre}
                  onChange={({ detail }) => setFormGenre(detail.selectedOption)}
                  options={[
                    { value: "Action", label: "Action" },
                    { value: "Sci-Fi/Adventure", label: "Sci-Fi/Adventure" },
                    { value: "Drama", label: "Drama" },
                    { value: "Comedy", label: "Comedy" },
                    { value: "Horror", label: "Horror" },
                    { value: "Animation", label: "Animation" }
                  ]}
                />
              </FormField>
            </ColumnLayout>
            <ColumnLayout columns={2}>
              <FormField label="Release Status">
                <Select
                  selectedOption={formStatus}
                  onChange={({ detail }) => setFormStatus(detail.selectedOption)}
                  options={[
                    { value: "now_showing", label: "Now Showing" },
                    { value: "coming_soon", label: "Coming Soon" },
                    { value: "ended", label: "Ended" }
                  ]}
                />
              </FormField>
              <FormField label="Censor Rating">
                <Select
                  selectedOption={formCensor}
                  onChange={({ detail }) => setFormCensor(detail.selectedOption)}
                  options={CENSOR_OPTIONS}
                />
              </FormField>
            </ColumnLayout>
            <ColumnLayout columns={2}>
              <FormField label="Supported Formats" description="Select one or more available formats">
                <Multiselect
                  selectedOptions={formFormats}
                  onChange={({ detail }) => setFormFormats(detail.selectedOptions)}
                  options={FORMAT_OPTIONS}
                  placeholder="Choose formats..."
                  deselectAriaLabel={option => `Remove ${option.label}`}
                />
              </FormField>
              <FormField label="Release Date">
                <Input type="date" value={formReleaseDate} onChange={({ detail }) => setFormReleaseDate(detail.value)} />
              </FormField>
            </ColumnLayout>
          </SpaceBetween>
        </Modal>

        {/* Edit Movie Modal */}
        <Modal
          visible={editModalOpen}
          onDismiss={() => setEditModalOpen(false)}
          header={`Edit Movie: ${selectedItem?.title || ""}`}
          closeAriaLabel="Close modal"
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button variant="link" onClick={() => setEditModalOpen(false)}>Cancel</Button>
                <Button variant="primary" onClick={handleSaveEdit} loading={editMutation.isPending}>Save Changes</Button>
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
            <PosterUploadControl
              posterUrl={formPosterUrl}
              onChangePosterUrl={setFormPosterUrl}
              onFileSelect={handlePosterUpload}
              isUploading={isUploadingPoster}
              uploadError={uploadError}
              fileInputRef={editFileInputRef}
              title={formTitle}
            />
            <FormField label="Movie Title">
              <Input value={formTitle} onChange={({ detail }) => setFormTitle(detail.value)} />
            </FormField>
            <ColumnLayout columns={2}>
              <FormField label="Duration (Minutes)">
                <Input type="number" value={formDuration} onChange={({ detail }) => setFormDuration(detail.value)} />
              </FormField>
              <FormField label="Genre">
                <Select
                  selectedOption={formGenre}
                  onChange={({ detail }) => setFormGenre(detail.selectedOption)}
                  options={[
                    { value: "Action", label: "Action" },
                    { value: "Sci-Fi/Adventure", label: "Sci-Fi/Adventure" },
                    { value: "Drama", label: "Drama" },
                    { value: "Comedy", label: "Comedy" },
                    { value: "Horror", label: "Horror" },
                    { value: "Animation", label: "Animation" }
                  ]}
                />
              </FormField>
            </ColumnLayout>
            <ColumnLayout columns={2}>
              <FormField label="Release Status">
                <Select
                  selectedOption={formStatus}
                  onChange={({ detail }) => setFormStatus(detail.selectedOption)}
                  options={[
                    { value: "now_showing", label: "Now Showing" },
                    { value: "coming_soon", label: "Coming Soon" },
                    { value: "ended", label: "Ended" }
                  ]}
                />
              </FormField>
              <FormField label="Censor Rating">
                <Select
                  selectedOption={formCensor}
                  onChange={({ detail }) => setFormCensor(detail.selectedOption)}
                  options={CENSOR_OPTIONS}
                />
              </FormField>
            </ColumnLayout>
            <ColumnLayout columns={2}>
              <FormField label="Supported Formats" description="Select one or more available formats">
                <Multiselect
                  selectedOptions={formFormats}
                  onChange={({ detail }) => setFormFormats(detail.selectedOptions)}
                  options={FORMAT_OPTIONS}
                  placeholder="Choose formats..."
                  deselectAriaLabel={option => `Remove ${option.label}`}
                />
              </FormField>
              <FormField label="Release Date">
                <Input type="date" value={formReleaseDate} onChange={({ detail }) => setFormReleaseDate(detail.value)} />
              </FormField>
            </ColumnLayout>
          </SpaceBetween>
        </Modal>

        {/* View Details Modal */}
        {selectedItem && (
          <Modal
            visible={detailsModalOpen}
            onDismiss={() => setDetailsModalOpen(false)}
            header={`Movie Details: ${selectedItem.title}`}
            closeAriaLabel="Close modal"
            footer={
              <Box float="right">
                <Button onClick={() => setDetailsModalOpen(false)}>Close</Button>
              </Box>
            }
          >
            <Container>
              <SpaceBetween size="m">
                <div style={{ display: "flex", gap: "20px", alignItems: "flex-start" }}>
                  <MoviePoster title={selectedItem.title} posterUrl={selectedItem.posterUrl} />
                  <SpaceBetween size="xxs">
                    <Box variant="h2">{selectedItem.title}</Box>
                    <Box color="text-body-secondary">
                      {selectedItem.genre || "General"} • {selectedItem.durationMinutes || 0} min • Rating: ★ {selectedItem.rating || "8.0"}
                    </Box>
                    <SpaceBetween direction="horizontal" size="xs">
                      {renderStatusBadge(selectedItem)}
                      {renderCensorRating(selectedItem)}
                      {renderFormats(selectedItem)}
                    </SpaceBetween>
                  </SpaceBetween>
                </div>
                <ColumnLayout columns={2} variant="text-grid">
                  <div>
                    <Box variant="awsui-key-label">Release Date</Box>
                    <Box>{renderReleaseDate(selectedItem)}</Box>
                  </div>
                  <div>
                    <Box variant="awsui-key-label">Audio / Subtitles</Box>
                    <div style={{ display: "flex", alignItems: "center", gap: "6px", marginTop: "2px", flexWrap: "wrap" }}>
                      <Box>{selectedItem.audioLanguage || "Khmer"} / {selectedItem.subtitleLanguage || "English"}</Box>
                      {((selectedItem.audioLanguage || "").toLowerCase().includes("kh") || (selectedItem.audioLanguage || "").toLowerCase().includes("khmer")) && (
                        <img src={getMinioIconUrl("AUDIO_KH.png")} onError={e => { e.currentTarget.src = "/icons/AUDIO_KH.png"; }} alt="Audio KH" title="Audio: Khmer" style={{ height: "15px", width: "auto", background: "#111827", padding: "1px 4px", borderRadius: "3px", verticalAlign: "middle" }} />
                      )}
                      {((selectedItem.audioLanguage || "").toLowerCase().includes("en") || (selectedItem.audioLanguage || "").toLowerCase().includes("english")) && (
                        <img src={getMinioIconUrl("AUDIO_EN.png")} onError={e => { e.currentTarget.src = "/icons/AUDIO_EN.png"; }} alt="Audio EN" title="Audio: English" style={{ height: "15px", width: "auto", background: "#111827", padding: "1px 4px", borderRadius: "3px", verticalAlign: "middle" }} />
                      )}
                      {((selectedItem.subtitleLanguage || "").toLowerCase().includes("kh") || (selectedItem.subtitleLanguage || "").toLowerCase().includes("khmer")) && (
                        <img src={getMinioIconUrl("SUB_KH.png")} onError={e => { e.currentTarget.src = "/icons/SUB_KH.png"; }} alt="Sub KH" title="Subtitle: Khmer" style={{ height: "15px", width: "auto", background: "#111827", padding: "1px 4px", borderRadius: "3px", verticalAlign: "middle" }} />
                      )}
                    </div>
                  </div>
                  <div>
                    <Box variant="awsui-key-label">Status</Box>
                    <StatusIndicator type={selectedItem.isActive ? "success" : "stopped"}>
                      {selectedItem.isActive ? "Active in POS Catalog" : "Inactive"}
                    </StatusIndicator>
                  </div>
                  <div>
                    <Box variant="awsui-key-label">Movie ID</Box>
                    <Box fontSize="body-s" color="text-body-secondary">{selectedItem.movieId}</Box>
                  </div>
                </ColumnLayout>
                {/* Screening Branches & Showtimes */}
                <div>
                  <Box variant="awsui-key-label" margin={{ bottom: "xs" }}>Screening Branches & Showtimes</Box>
                  {(() => {
                    const sts = showtimesByMovie[selectedItem.movieId] || [];
                    if (sts.length === 0) {
                      return (
                        <Box color="text-body-secondary">
                          {selectedItem.releaseStatus === "coming_soon" 
                            ? "Coming Soon — No branch showtimes scheduled yet." 
                            : "No active showtimes currently scheduled for this movie."}
                        </Box>
                      );
                    }
                    const grouped = {};
                    sts.forEach(st => {
                      const bName = st.branchName || "Legend Cinema";
                      if (!grouped[bName]) grouped[bName] = [];
                      grouped[bName].push(st);
                    });
                    return (
                      <SpaceBetween size="s">
                        {Object.entries(grouped).map(([bName, showtimesList]) => (
                          <div
                            key={bName}
                            style={{
                              background: "#f8fafc",
                              border: "1px solid #e2e8f0",
                              borderRadius: "6px",
                              padding: "10px 14px",
                            }}
                          >
                            <div style={{ fontWeight: 600, fontSize: "13px", color: "#0f172a", marginBottom: "6px" }}>
                              📍 {bName}
                            </div>
                            <div style={{ display: "flex", flexWrap: "wrap", gap: "8px" }}>
                              {showtimesList.map((s, idx) => {
                                const timeStr = s.startsAt
                                  ? new Date(s.startsAt).toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit" })
                                  : (s.startTime || "-");
                                return (
                                  <span
                                    key={s.showtimeId || idx}
                                    style={{
                                      background: "#ffffff",
                                      border: "1px solid #cbd5e1",
                                      borderRadius: "4px",
                                      padding: "3px 8px",
                                      fontSize: "12px",
                                      display: "inline-flex",
                                      alignItems: "center",
                                      gap: "6px"
                                    }}
                                  >
                                    <b>{timeStr}</b>
                                    <span style={{ color: "#64748b" }}>•</span>
                                    <span>{s.auditoriumName || "Hall"}</span>
                                    {s.basePrice != null && (
                                      <>
                                        <span style={{ color: "#64748b" }}>•</span>
                                        <span style={{ fontWeight: 600, color: "#0972d3" }}>${Number(s.basePrice).toFixed(2)}</span>
                                      </>
                                    )}
                                  </span>
                                );
                              })}
                            </div>
                          </div>
                        ))}
                      </SpaceBetween>
                    );
                  })()}
                </div>

                {selectedItem.teaserText && (
                  <div>
                    <Box variant="awsui-key-label">Teaser / Synopsis</Box>
                    <Box variant="p">{selectedItem.teaserText}</Box>
                  </div>
                )}
              </SpaceBetween>
            </Container>
          </Modal>
        )}
      </SpaceBetween>
    </ContentLayout>
  );
}

export default function MoviesContent() {
  return (
    <QueryWrapper>
      <MoviesContentInner />
    </QueryWrapper>
  );
}
