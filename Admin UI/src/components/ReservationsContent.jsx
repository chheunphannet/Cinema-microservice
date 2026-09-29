import * as React from "react";
import ContentLayout from "@cloudscape-design/components/content-layout";
import Header from "@cloudscape-design/components/header";
import Table from "@cloudscape-design/components/table";
import Button from "@cloudscape-design/components/button";
import SpaceBetween from "@cloudscape-design/components/space-between";
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
import RadioGroup from "@cloudscape-design/components/radio-group";
import Form from "@cloudscape-design/components/form";
import Badge from "@cloudscape-design/components/badge";
import { useCollection } from "@cloudscape-design/collection-hooks";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../lib/apiClient";
import { QueryWrapper } from "./QueryWrapper";

function ReservationsContentInner() {
  const queryClient = useQueryClient();
  const { data, isLoading, error, refetch, isFetching } = useQuery({
    queryKey: ['reservationsSearch'],
    queryFn: () => apiClient('/api/v1/admin/bookings/search').catch(() => ({ items: [] })),
  });

  const [notifications, setNotifications] = React.useState([]);

  // Modal states
  const [refundModalOpen, setRefundModalOpen] = React.useState(false);
  
  // Form states
  const [refundType, setRefundType] = React.useState("full");
  const [refundAmount, setRefundAmount] = React.useState("");
  const [reasonCode, setReasonCode] = React.useState({ value: "customer_request", label: "Customer Request" });

  const bookings = React.useMemo(() => {
    if (!data) return [];
    if (Array.isArray(data)) return data;
    if (Array.isArray(data.items)) return data.items;
    if (Array.isArray(data.data)) return data.data;
    return [];
  }, [data]);

  const { items, actions, filteredItemsCount, collectionProps, filterProps, paginationProps } = useCollection(
    bookings,
    {
      filtering: {
        empty: (
          <Box textAlign="center" color="inherit">
            <b>No bookings found</b>
            <Box padding={{ bottom: "s" }} variant="p" color="inherit">
              No bookings are currently available.
            </Box>
          </Box>
        ),
        noMatch: (
          <Box textAlign="center" color="inherit">
            <SpaceBetween size="xxs">
              <b>No matches found</b>
              <Box variant="p" color="inherit">
                We couldn't find any booking matching your search criteria.
              </Box>
              <Button onClick={() => actions.setFiltering("")}>Clear filter</Button>
            </SpaceBetween>
          </Box>
        ),
        filteringFunction: (item, filteringText) => {
          if (!filteringText) return true;
          const text = String(filteringText).toLowerCase();
          const ref = String(item.bookingReference || item.bookingRef || "").toLowerCase();
          const email = String(item.customerEmail || "").toLowerCase();
          const phone = String(item.customerPhone || "").toLowerCase();
          const name = String(item.customerName || "").toLowerCase();
          const movie = String(item.movieTitle || "").toLowerCase();
          const branch = String(item.branchName || "").toLowerCase();
          
          return (
            ref.includes(text) ||
            email.includes(text) ||
            phone.includes(text) ||
            name.includes(text) ||
            movie.includes(text) ||
            branch.includes(text)
          );
        },
      },
      pagination: { pageSize: 10 },
      sorting: {},
      selection: { trackBy: item => item.bookingReference || item.bookingRef || item.reservationId || item.id },
    }
  );

  const selectedItem = collectionProps.selectedItems?.[0] || null;
  const selectedCount = collectionProps.selectedItems?.length || 0;

  const openRefundModal = () => {
    setRefundType("full");
    setRefundAmount(selectedItem?.totalAmount ? String(selectedItem.totalAmount) : (selectedItem?.totalPaid ? String(selectedItem.totalPaid) : ""));
    setReasonCode({ value: "customer_request", label: "Customer Request" });
    setRefundModalOpen(true);
  };

  const handleIssueRefund = async (e) => {
    e.preventDefault();
    if (!selectedItem) return;
    try {
      await apiClient(`/api/v1/admin/bookings/${selectedItem.reservationId}/refund`, {
        method: "POST",
        body: JSON.stringify({
          refundAmount: parseFloat(refundAmount),
          reasonCode: reasonCode.value
        })
      });
      setRefundModalOpen(false);
      setNotifications([{ type: "success", content: `Refund issued successfully.`, dismissible: true }]);
      refetch();
    } catch (err) {
      setNotifications([{ type: "error", content: err.message, dismissible: true }]);
    }
  };

  const handleReissueTicket = async () => {
    if (!selectedItem) return;
    try {
      await apiClient(`/api/v1/admin/bookings/${selectedItem.reservationId}/reissue-ticket`, {
        method: "POST",
        body: JSON.stringify({ regenerateQrTokens: false })
      });
      setNotifications([{ type: "success", content: "Tickets re-issued successfully.", dismissible: true }]);
    } catch (err) {
      setNotifications([{ type: "error", content: err.message, dismissible: true }]);
    }
  };

  React.useEffect(() => {
    const total = selectedItem?.totalAmount ?? selectedItem?.totalPaid;
    if (refundType === "full" && total) {
      setRefundAmount(String(total));
    } else if (refundType === "partial") {
      setRefundAmount("");
    }
  }, [refundType, selectedItem]);

  const formatShowtime = (val) => {
    if (!val) return "-";
    try {
      const d = new Date(val);
      if (isNaN(d.getTime())) return val;
      return d.toLocaleString("en-US", {
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      });
    } catch {
      return val;
    }
  };

  return (
    <ContentLayout
      header={
        <Header
          variant="h1"
          description="Master Booking Search, Reservations, and Customer Transaction Management."
          info={
            <StatusIndicator type={error ? "error" : isFetching ? "loading" : "success"}>
              {error ? "Sync Error" : isFetching ? "Syncing..." : "Live Data"}
            </StatusIndicator>
          }
          actions={
            <SpaceBetween direction="horizontal" size="xs">
              <Button iconName="refresh" onClick={() => refetch()} loading={isFetching}>
                Refresh
              </Button>
              <Button disabled={selectedCount === 0} onClick={handleReissueTicket}>
                Re-issue Ticket
              </Button>
              <Button disabled={selectedCount === 0} onClick={openRefundModal}>
                Issue Refund
              </Button>
            </SpaceBetween>
          }
        >
          Reservations & Bookings
        </Header>
      }
    >
      <SpaceBetween size="l">
        {notifications.length > 0 && <Flashbar items={notifications} />}

        {error && (
          <Alert type="error" header="Failed to load bookings">
            {error.message}
          </Alert>
        )}

        <Table
          {...collectionProps}
          selectionType="single"
          columnDefinitions={[
            {
              id: "bookingRef",
              header: "Booking Ref",
              sortingField: "bookingReference",
              cell: (item) => (
                <span style={{ fontWeight: 700, fontFamily: "monospace", color: "#0972d3", fontSize: "13px" }}>
                  {item.bookingReference || item.bookingRef || "-"}
                </span>
              ),
              isRowHeader: true,
            },
            {
              id: "customer",
              header: "Customer",
              cell: (item) => (
                <div>
                  <div style={{ fontWeight: 600 }}>{item.customerName || item.customerEmail?.split("@")[0] || "Guest Customer"}</div>
                  <div style={{ fontSize: "12px", color: "#545b64" }}>
                    {item.customerEmail || "-"}
                    {item.customerPhone ? ` • ${item.customerPhone}` : ""}
                  </div>
                </div>
              ),
            },
            {
              id: "movie",
              header: "Movie & Showtime",
              cell: (item) => (
                <div>
                  <div style={{ fontWeight: 600, color: "#16191f" }}>
                    {item.movieTitle || item.movie || "-"}
                  </div>
                  <div style={{ fontSize: "12px", color: "#545b64" }}>
                    🕒 {formatShowtime(item.showtimeStart || item.showtime)}
                  </div>
                </div>
              ),
            },
            {
              id: "branch",
              header: "Cinema & Hall",
              cell: (item) => (
                <div>
                  <div style={{ fontWeight: 500 }}>{item.branchName || item.branch || "Legend Cinema"}</div>
                  {item.auditoriumName && (
                    <div style={{ fontSize: "12px", color: "#64748b" }}>
                      {item.auditoriumName}
                    </div>
                  )}
                </div>
              ),
            },
            {
              id: "totalPaid",
              header: "Total Paid",
              sortingField: "totalAmount",
              cell: (item) => {
                const total = item.totalAmount ?? item.totalPaid;
                return (
                  <span style={{ fontWeight: 700, color: "#16191f" }}>
                    {total != null ? `$${Number(total).toFixed(2)}` : "-"}
                  </span>
                );
              },
            },
            {
              id: "status",
              header: "Status",
              sortingField: "status",
              cell: (item) => {
                const s = String(item.status || "confirmed").toLowerCase();
                let type = "info";
                let label = item.status ? item.status.replace(/_/g, " ").replace(/\b\w/g, (c) => c.toUpperCase()) : "Confirmed";
                if (s === "confirmed" || s === "paid" || s === "completed") {
                  type = "success";
                } else if (s === "cancelled" || s === "failed") {
                  type = "error";
                } else if (s === "hold" || s === "pending" || s === "payment_pending") {
                  type = "pending";
                } else if (s === "expired") {
                  type = "stopped";
                } else if (s === "refunded") {
                  type = "info";
                }
                return (
                  <StatusIndicator type={type}>
                    {label}
                  </StatusIndicator>
                );
              },
            },
          ]}
          items={items}
          loading={isLoading}
          loadingText="Loading bookings..."
          filter={
            <TextFilter
              {...filterProps}
              filteringPlaceholder="Search by booking ref, email, phone, movie..."
              countText={`${filteredItemsCount} ${filteredItemsCount === 1 ? 'match' : 'matches'}`}
            />
          }
          pagination={<Pagination {...paginationProps} />}
          empty={
            <Box textAlign="center" color="inherit">
              <b>No bookings found</b>
              <Box padding={{ bottom: "s" }} variant="p" color="inherit">
                No bookings available to display.
              </Box>
            </Box>
          }
        />

        <Modal
          visible={refundModalOpen}
          onDismiss={() => setRefundModalOpen(false)}
          header={`Issue Refund: ${selectedItem?.bookingReference || selectedItem?.bookingRef || selectedItem?.reservationId || ""}`}
          closeAriaLabel="Close modal"
          footer={
            <Box float="right">
              <SpaceBetween direction="horizontal" size="xs">
                <Button variant="link" onClick={() => setRefundModalOpen(false)}>Cancel</Button>
                <Button variant="primary" onClick={handleIssueRefund}>Confirm Refund</Button>
              </SpaceBetween>
            </Box>
          }
        >
          <Form>
            <SpaceBetween direction="vertical" size="m">
              <FormField label="Refund Type">
                <RadioGroup
                  value={refundType}
                  onChange={({ detail }) => setRefundType(detail.value)}
                  items={[
                    { value: "full", label: "Full Refund" },
                    { value: "partial", label: "Partial Refund" }
                  ]}
                />
              </FormField>
              <FormField label="Refund Amount ($)">
                <Input
                  value={refundAmount}
                  onChange={({ detail }) => setRefundAmount(detail.value)}
                  type="number"
                  disabled={refundType === "full"}
                />
              </FormField>
              <FormField label="Reason">
                <Select
                  selectedOption={reasonCode}
                  onChange={({ detail }) => setReasonCode(detail.selectedOption)}
                  options={[
                    { value: "customer_request", label: "Customer Request" },
                    { value: "show_cancelled", label: "Show Cancelled" },
                    { value: "technical_issue", label: "Technical Issue" },
                    { value: "fraud", label: "Fraudulent Activity" }
                  ]}
                />
              </FormField>
            </SpaceBetween>
          </Form>
        </Modal>
      </SpaceBetween>
    </ContentLayout>
  );
}

export default function ReservationsContent() {
  return (
    <QueryWrapper>
      <ReservationsContentInner />
    </QueryWrapper>
  );
}
