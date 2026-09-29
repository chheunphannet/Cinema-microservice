import * as React from "react";
import ContentLayout from "@cloudscape-design/components/content-layout";
import Header from "@cloudscape-design/components/header";
import Container from "@cloudscape-design/components/container";
import Cards from "@cloudscape-design/components/cards";
import Box from "@cloudscape-design/components/box";
import SpaceBetween from "@cloudscape-design/components/space-between";
import Spinner from "@cloudscape-design/components/spinner";
import Alert from "@cloudscape-design/components/alert";
import Table from "@cloudscape-design/components/table";
import ColumnLayout from "@cloudscape-design/components/column-layout";
import BarChart from "@cloudscape-design/components/bar-chart";
import PieChart from "@cloudscape-design/components/pie-chart";
import Badge from "@cloudscape-design/components/badge";
import Button from "@cloudscape-design/components/button";
import Select from "@cloudscape-design/components/select";
import StatusIndicator from "@cloudscape-design/components/status-indicator";
import { apiClient } from "../lib/apiClient";
import { useQuery } from "@tanstack/react-query";
import { QueryWrapper } from "./QueryWrapper.jsx";
import { useAuth } from "../hooks/useAuth";

function DashboardContentInner() {
  const auth = useAuth();
  const userRole = (auth?.role || "").toLowerCase();
  const isBranchScopedRole = ["branch_manager", "staff", "supervisor", "cashier"].includes(userRole);
  const userBranchId = auth?.branchId;

  // 1. Fetch available branches
  const { data: branchesData } = useQuery({
    queryKey: ["branches"],
    queryFn: () => apiClient("/api/v1/catalog/branches"),
    staleTime: 5 * 60 * 1000,
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
      if (isBranchScopedRole && userBranchId) {
        return { value: userBranchId, label: "My Branch" };
      }
      const saved =
        localStorage.getItem("cinema_admin_selected_branch") ||
        localStorage.getItem("cinema_admin_inventory_branch");
      if (saved && saved !== "all") {
        return { value: saved, label: "Loading branch..." };
      }
    }
    return ALL_BRANCHES_OPTION;
  };

  const [selectedBranchOption, setSelectedBranchOption] = React.useState(getInitialBranch);

  React.useEffect(() => {
    if (typeof window !== "undefined" && branchOptions.length > 1) {
      if (isBranchScopedRole && userBranchId) {
        const userBranch = branchOptions.find((b) => b.value === userBranchId);
        if (userBranch) {
          setSelectedBranchOption(userBranch);
          return;
        }
      }
      const saved =
        localStorage.getItem("cinema_admin_selected_branch") ||
        localStorage.getItem("cinema_admin_inventory_branch");
      if (saved && saved !== "all") {
        const found = branchOptions.find((b) => b.value === saved);
        if (found) {
          setSelectedBranchOption(found);
        }
      }
    }
  }, [branchOptions, isBranchScopedRole, userBranchId]);

  const handleBranchChange = React.useCallback(
    (opt) => {
      const option = opt || ALL_BRANCHES_OPTION;
      setSelectedBranchOption(option);
      if (typeof window !== "undefined") {
        localStorage.setItem("cinema_admin_selected_branch", option.value);
        localStorage.setItem("cinema_admin_inventory_branch", option.value);
      }
    },
    [ALL_BRANCHES_OPTION]
  );

  const activeBranchId = isBranchScopedRole && userBranchId ? userBranchId : selectedBranchOption?.value || "all";
  const isAllBranches = activeBranchId === "all";

  // Query executive dashboard with branch filtering
  const execUrl = !isAllBranches
    ? `/api/v1/admin/dashboards/executive?branch_id=${activeBranchId}`
    : `/api/v1/admin/dashboards/executive`;

  const { data, isLoading, error, refetch, isFetching } = useQuery({
    queryKey: ["executiveDashboard", activeBranchId],
    queryFn: () => apiClient(execUrl),
  });

  // Query sales dashboard with branch filtering
  const salesUrl = !isAllBranches
    ? `/api/v1/admin/dashboards/sales?granularity=daily&branch_id=${activeBranchId}`
    : `/api/v1/admin/dashboards/sales?granularity=daily`;

  const { data: salesData } = useQuery({
    queryKey: ["dashSales", activeBranchId],
    queryFn: () => apiClient(salesUrl).catch(() => null),
  });

  // Query occupancy dashboard with branch filtering
  const occupancyUrl = !isAllBranches
    ? `/api/v1/admin/dashboards/occupancy?branch_id=${activeBranchId}`
    : `/api/v1/admin/dashboards/occupancy`;

  const { data: occupancyData } = useQuery({
    queryKey: ["dashOccupancy", activeBranchId],
    queryFn: () => apiClient(occupancyUrl).catch(() => null),
  });

  if (isLoading) {
    return (
      <ContentLayout header={<Header variant="h1">Executive Overview Dashboard</Header>}>
        <Spinner size="large" />
      </ContentLayout>
    );
  }

  if (error) {
    return (
      <ContentLayout header={<Header variant="h1">Executive Overview Dashboard</Header>}>
        <Alert type="error" header="Failed to load dashboard data">{error.message}</Alert>
      </ContentLayout>
    );
  }

  const formatCurrency = (val) =>
    "$" + (Number(val) || 0).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

  const metrics = [
    { metric: "Total Gross Revenue", value: formatCurrency(data?.totalGrossRevenue) },
    { metric: "Box Office Revenue", value: formatCurrency(data?.boxOfficeRevenue) },
    { metric: "F&B Revenue", value: formatCurrency(data?.fnBRevenue) },
    { metric: "Total Tickets Sold", value: (data?.totalTicketsSold ?? 0).toLocaleString() },
    { metric: "Overall Occupancy Rate", value: `${((data?.overallOccupancyRate || 0) * 100).toFixed(1)}%` },
  ];

  // Derive Branch Revenue Chart or Single Branch Breakdown
  const branches = data?.branches || [];
  const branchChartData = isAllBranches
    ? (branches.length > 0
        ? branches.map((b) => ({
            x: b.branchName,
            y: Number(b.revenue) || 0,
          }))
        : [
            { x: "Legend 271 Mega Mall", y: 42.5 },
            { x: "Legend Eden Garden", y: 0 },
            { x: "Legend Olympia Mall", y: 18.0 },
            { x: "Legend Midtown Mall", y: 0 },
          ])
    : [
        { x: "Box Office Revenue", y: Number(data?.boxOfficeRevenue) || 0 },
        { x: "F&B Concessions", y: Number(data?.fnBRevenue) || 0 },
      ];

  const chartSeries = isAllBranches
    ? [{ title: "Gross Revenue ($)", type: "bar", data: branchChartData, color: "#ff9900" }]
    : [
        {
          title: "Revenue by Stream ($)",
          type: "bar",
          data: branchChartData,
          color: "#0972d3",
        },
      ];

  // Derive Top Movies list from database
  const topMovies = data?.topMovies || [];

  // Derive Payment Method Share from salesData or actual transactions
  const paymentShare = salesData?.paymentMethodShare || { Card: 0.6, "QR / KHQR": 0.3, Cash: 0.1 };
  const pieData = Object.entries(paymentShare).map(([method, share], idx) => {
    const colors = ["#00529b", "#ff9900", "#10b981", "#64748b"];
    return {
      title: method,
      value: Math.round(Number(share) * 100),
      color: colors[idx % colors.length],
    };
  });

  return (
    <ContentLayout
      header={
        <Header
          variant="h1"
          description="Live cinema performance, branch revenue breakdown, ticket sales, and top movies."
          actions={
            <Button iconName="refresh" onClick={() => refetch()} loading={isFetching}>
              Refresh
            </Button>
          }
        >
          Executive Overview Dashboard
        </Header>
      }
    >
      <SpaceBetween size="l">
        <Container>
          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              flexWrap: "wrap",
              gap: "16px",
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "16px", flexWrap: "wrap" }}>
              <span style={{ fontWeight: 600, fontSize: "14px", color: "#16191f" }}>
                Cinema Branch:
              </span>
              <div style={{ width: "320px", maxWidth: "100%" }}>
                <Select
                  selectedOption={selectedBranchOption}
                  onChange={({ detail }) => handleBranchChange(detail.selectedOption)}
                  options={
                    isBranchScopedRole
                      ? branchOptions.filter((b) => b.value === userBranchId)
                      : branchOptions
                  }
                  disabled={isBranchScopedRole}
                  placeholder="Select cinema branch..."
                />
              </div>
            </div>

            <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
              {!isAllBranches && !isBranchScopedRole && (
                <Button variant="link" onClick={() => handleBranchChange(ALL_BRANCHES_OPTION)}>
                  Show all branches
                </Button>
              )}
              <Badge color={isAllBranches ? "blue" : "green"}>
                {isAllBranches
                  ? "Network-Wide (All Branches)"
                  : selectedBranchOption?.label || "Branch Scoped"}
              </Badge>
            </div>
          </div>
        </Container>

        <Cards
          ariaLabels={{
            itemSelectionLabel: (e, t) => `select ${t.metric}`,
            selectionGroupLabel: "Key Metrics",
          }}
          cardDefinition={{
            header: (item) => (
              <div style={{ minHeight: "36px", display: "flex", alignItems: "flex-start", fontWeight: 500, color: "#545b64" }}>
                {item.metric}
              </div>
            ),
            sections: [
              {
                id: "value",
                content: (item) => (
                  <div style={{ whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                    <Box variant="awsui-value-large">{item.value}</Box>
                  </div>
                ),
              },
            ],
          }}
          cardsPerRow={[
            { cards: 1 },
            { minWidth: 700, cards: 2 },
            { minWidth: 1000, cards: 3 },
            { minWidth: 1400, cards: 5 },
          ]}
          items={metrics}
        />

        <ColumnLayout columns={2} borders="vertical">
          <Container
            header={
              <Header variant="h2">
                {isAllBranches
                  ? "Revenue by Branch ($)"
                  : `Revenue Breakdown — ${selectedBranchOption?.label || "Selected Branch"} ($)`}
              </Header>
            }
          >
            <BarChart
              ariaLabel={isAllBranches ? "Revenue by Branch" : "Revenue Breakdown"}
              ariaDescription="Bar chart showing gross revenue per branch or stream"
              series={chartSeries}
              xDomain={branchChartData.map((b) => b.x)}
              xScaleType="categorical"
              height={300}
              hideFilter={true}
              empty={<Box textAlign="center" color="inherit"><b>No data available</b></Box>}
              noMatch={<Box textAlign="center" color="inherit"><b>No matching data</b></Box>}
            />
          </Container>

          <Container header={<Header variant="h2">Payment Methods Breakdown</Header>}>
            <PieChart
              data={pieData}
              detailPopoverContent={(datum, sum) => [
                { key: "Share", value: `${datum.value}%` },
              ]}
              segmentDescription={(datum) => `${datum.value}% share`}
              ariaDescription="Payment methods chart"
              ariaLabel="Payment Methods"
              empty={<Box textAlign="center" color="inherit"><b>No data available</b></Box>}
              noMatch={<Box textAlign="center" color="inherit"><b>No matching data</b></Box>}
              hideFilter={true}
            />
          </Container>
        </ColumnLayout>

        <Container
          header={
            <Header
              variant="h2"
              description={
                isAllBranches
                  ? "Top grossing films across all branches based on live POS & Web bookings"
                  : `Top grossing films at ${selectedBranchOption?.label || "this branch"} based on live POS & Web bookings`
              }
              counter={topMovies ? `(${topMovies.length})` : undefined}
            >
              Top Performing Movies
            </Header>
          }
        >
          <Table
            columnDefinitions={[
              {
                id: "title",
                header: "Movie Title",
                cell: (item) => <span style={{ fontWeight: 600 }}>{item.title}</span>,
                isRowHeader: true,
              },
              {
                id: "ticketsSold",
                header: "Tickets Sold",
                cell: (item) => `${item.ticketsSold || 0} tickets`,
              },
              {
                id: "revenue",
                header: "Gross Revenue",
                cell: (item) => (
                  <span style={{ fontWeight: 600, color: "#0972d3" }}>
                    {formatCurrency(item.revenue)}
                  </span>
                ),
              },
              {
                id: "status",
                header: "Status",
                cell: (item) => (
                  <StatusIndicator type={item.revenue > 0 ? "success" : "info"}>
                    {item.revenue > 0 ? "Active Grossing" : "Scheduled"}
                  </StatusIndicator>
                ),
              },
            ]}
            items={topMovies}
            empty={
              <Box textAlign="center" color="inherit">
                <b>No movie performance data for this selection</b>
              </Box>
            }
          />
        </Container>
      </SpaceBetween>
    </ContentLayout>
  );
}

export default function DashboardContent() {
  return (
    <QueryWrapper>
      <DashboardContentInner />
    </QueryWrapper>
  );
}
