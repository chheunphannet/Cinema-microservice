import * as React from "react";
import ContentLayout from "@cloudscape-design/components/content-layout";
import Header from "@cloudscape-design/components/header";
import SpaceBetween from "@cloudscape-design/components/space-between";
import Tabs from "@cloudscape-design/components/tabs";
import Table from "@cloudscape-design/components/table";
import Button from "@cloudscape-design/components/button";
import Box from "@cloudscape-design/components/box";
import Alert from "@cloudscape-design/components/alert";
import Flashbar from "@cloudscape-design/components/flashbar";
import Modal from "@cloudscape-design/components/modal";
import FormField from "@cloudscape-design/components/form-field";
import Input from "@cloudscape-design/components/input";
import Select from "@cloudscape-design/components/select";
import Toggle from "@cloudscape-design/components/toggle";
import AttributeEditor from "@cloudscape-design/components/attribute-editor";
import Badge from "@cloudscape-design/components/badge";
import StatusIndicator from "@cloudscape-design/components/status-indicator";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../lib/apiClient";
import { QueryWrapper } from "./QueryWrapper.jsx";

const RULE_TYPE_OPTIONS = [
  { label: "Screen Format Surcharge (3D, IMAX, 4DX)", value: "format_surcharge" },
  { label: "Day of Week Discount / Surge", value: "day_of_week" },
  { label: "Matinee Early Bird Discount", value: "matinee" },
  { label: "Weekend Prime Surge", value: "weekend_surge" },
];

const ADJUSTMENT_TYPE_OPTIONS = [
  { label: "Fixed Amount ($)", value: "fixed_amount" },
  { label: "Percentage (%)", value: "percentage" },
];

const DAY_OF_WEEK_OPTIONS = [
  { label: "Sunday", value: 0 },
  { label: "Monday", value: 1 },
  { label: "Tuesday", value: 2 },
  { label: "Wednesday", value: 3 },
  { label: "Thursday", value: 4 },
  { label: "Friday", value: 5 },
  { label: "Saturday", value: 6 },
];

const SEAT_TYPE_OPTIONS = [
  { label: "Standard", value: "standard" },
  { label: "VIP", value: "vip" },
  { label: "Couple", value: "couple" },
  { label: "Accessible", value: "accessible" },
];

// Helper to safely unwrap array responses from API
function unwrapList(data) {
  if (!data) return [];
  if (Array.isArray(data)) return data;
  if (Array.isArray(data.value)) return data.value;
  if (Array.isArray(data.items)) return data.items;
  if (Array.isArray(data.data)) return data.data;
  return [];
}

// --- Pricing Rules View ---
function PricingRulesView({ notifications, setNotifications }) {
  const queryClient = useQueryClient();
  const { data: rawRules, isLoading, error, refetch } = useQuery({
    queryKey: ["pricingRules"],
    queryFn: () => apiClient("/api/v1/admin/pricing/rules"),
  });

  const rules = React.useMemo(() => unwrapList(rawRules), [rawRules]);

  const [visible, setVisible] = React.useState(false);
  const [editingRule, setEditingRule] = React.useState(null);

  const [name, setName] = React.useState("");
  const [ruleType, setRuleType] = React.useState(RULE_TYPE_OPTIONS[0]);
  const [adjustmentType, setAdjustmentType] = React.useState(ADJUSTMENT_TYPE_OPTIONS[0]);
  const [adjustmentValue, setAdjustmentValue] = React.useState("0");
  const [screenTypeCode, setScreenTypeCode] = React.useState("");
  const [dayOfWeek, setDayOfWeek] = React.useState(DAY_OF_WEEK_OPTIONS[3]); // Wednesday default
  const [startTime, setStartTime] = React.useState("00:00:00");
  const [endTime, setEndTime] = React.useState("12:00:00");
  const [isActive, setIsActive] = React.useState(true);

  const resetForm = () => {
    setEditingRule(null);
    setName("");
    setRuleType(RULE_TYPE_OPTIONS[0]);
    setAdjustmentType(ADJUSTMENT_TYPE_OPTIONS[0]);
    setAdjustmentValue("0");
    setScreenTypeCode("");
    setDayOfWeek(DAY_OF_WEEK_OPTIONS[3]);
    setStartTime("00:00:00");
    setEndTime("12:00:00");
    setIsActive(true);
  };

  const openEdit = (rule) => {
    setEditingRule(rule);
    setName(rule.name || "");
    const rt = RULE_TYPE_OPTIONS.find((r) => r.value === rule.ruleType) || {
      label: rule.ruleType,
      value: rule.ruleType,
    };
    setRuleType(rt);
    const at = ADJUSTMENT_TYPE_OPTIONS.find((a) => a.value === rule.adjustmentType) || {
      label: rule.adjustmentType,
      value: rule.adjustmentType,
    };
    setAdjustmentType(at);
    setAdjustmentValue(rule.adjustmentValue != null ? rule.adjustmentValue.toString() : "0");
    setScreenTypeCode(rule.screenTypeCode || "");
    if (rule.dayOfWeek != null) {
      const dow = DAY_OF_WEEK_OPTIONS.find((d) => d.value === rule.dayOfWeek);
      if (dow) setDayOfWeek(dow);
    }
    setStartTime(rule.startTime || "00:00:00");
    setEndTime(rule.endTime || "12:00:00");
    setIsActive(rule.isActive ?? true);
    setVisible(true);
  };

  const saveMutation = useMutation({
    mutationFn: async () => {
      const payload = {
        name,
        ruleType: ruleType.value,
        adjustmentType: adjustmentType.value,
        adjustmentValue: parseFloat(adjustmentValue) || 0,
        screenTypeCode: ruleType.value === "format_surcharge" ? (screenTypeCode || null) : null,
        dayOfWeek: ruleType.value === "day_of_week" ? dayOfWeek?.value : null,
        startTime: ruleType.value === "matinee" ? startTime : null,
        endTime: ruleType.value === "matinee" ? endTime : null,
        isActive,
      };

      if (editingRule) {
        return apiClient(`/api/v1/admin/pricing/rules/${editingRule.ruleId}`, {
          method: "PUT",
          body: JSON.stringify(payload),
        });
      } else {
        return apiClient("/api/v1/admin/pricing/rules", {
          method: "POST",
          body: JSON.stringify(payload),
        });
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries(["pricingRules"]);
      setVisible(false);
      setNotifications([
        {
          type: "success",
          content: `Pricing rule "${name}" saved successfully.`,
          dismissible: true,
          id: Date.now().toString(),
        },
      ]);
    },
    onError: (err) => {
      setNotifications([
        {
          type: "error",
          content: err.message || "Failed to save pricing rule",
          dismissible: true,
          id: Date.now().toString(),
        },
      ]);
    },
  });

  const toggleStatusMutation = useMutation({
    mutationFn: async (rule) => {
      return apiClient(`/api/v1/admin/pricing/rules/${rule.ruleId}/status`, {
        method: "PATCH",
        body: JSON.stringify({ isActive: !rule.isActive }),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries(["pricingRules"]);
      setNotifications([
        {
          type: "success",
          content: "Rule status updated.",
          dismissible: true,
          id: Date.now().toString(),
        },
      ]);
    },
    onError: (err) => {
      setNotifications([
        {
          type: "error",
          content: err.message || "Failed to update rule status",
          dismissible: true,
          id: Date.now().toString(),
        },
      ]);
    },
  });

  const formatAdjustment = (item) => {
    const val = Number(item.adjustmentValue || 0);
    const sign = val > 0 ? "+" : "";
    if (item.adjustmentType === "percentage") {
      return `${sign}${val}%`;
    }
    return `${sign}$${val.toFixed(2)}`;
  };

  const formatCondition = (item) => {
    switch (item.ruleType) {
      case "format_surcharge":
        return `Screen Format: ${item.screenTypeCode || "Any"}`;
      case "day_of_week": {
        const dow = DAY_OF_WEEK_OPTIONS.find((d) => d.value === item.dayOfWeek);
        return `Every ${dow ? dow.label : `Day #${item.dayOfWeek}`}`;
      }
      case "matinee":
        return `Before ${item.endTime || "12:00"}`;
      case "weekend_surge":
        return "Friday evening - Sunday";
      default:
        return item.screenTypeCode || "-";
    }
  };

  return (
    <SpaceBetween size="l">
      {error && <Alert type="error" header="Failed to load rules">{error.message}</Alert>}

      <Table
        columnDefinitions={[
          {
            id: "name",
            header: "Rule Name",
            cell: (e) => <span style={{ fontWeight: 600 }}>{e.name}</span>,
            isRowHeader: true,
          },
          {
            id: "ruleType",
            header: "Rule Type",
            cell: (e) => {
              const color =
                e.ruleType === "format_surcharge"
                  ? "purple"
                  : e.ruleType === "day_of_week"
                  ? "blue"
                  : e.ruleType === "matinee"
                  ? "green"
                  : "grey";
              const label =
                RULE_TYPE_OPTIONS.find((r) => r.value === e.ruleType)?.label || e.ruleType;
              return <Badge color={color}>{label}</Badge>;
            },
          },
          {
            id: "condition",
            header: "Condition / Trigger",
            cell: (e) => formatCondition(e),
          },
          {
            id: "adjustment",
            header: "Price Adjustment",
            cell: (e) => {
              const val = Number(e.adjustmentValue || 0);
              const color = val > 0 ? "#0972d3" : "#1d8102";
              return (
                <span style={{ fontWeight: 700, color, fontSize: "14px" }}>
                  {formatAdjustment(e)}
                </span>
              );
            },
          },
          {
            id: "active",
            header: "Status",
            cell: (e) => (
              <StatusIndicator type={e.isActive ? "success" : "stopped"}>
                {e.isActive ? "Active" : "Inactive"}
              </StatusIndicator>
            ),
          },
          {
            id: "actions",
            header: "Actions",
            cell: (e) => (
              <SpaceBetween direction="horizontal" size="xs">
                <Button onClick={() => openEdit(e)}>Edit</Button>
                <Button
                  onClick={() => toggleStatusMutation.mutate(e)}
                  loading={toggleStatusMutation.isPending}
                >
                  {e.isActive ? "Deactivate" : "Activate"}
                </Button>
              </SpaceBetween>
            ),
          },
        ]}
        items={rules}
        loading={isLoading}
        loadingText="Loading dynamic pricing rules..."
        header={
          <Header
            actions={
              <SpaceBetween direction="horizontal" size="xs">
                <Button onClick={() => refetch()} iconName="refresh" />
                <Button
                  variant="primary"
                  onClick={() => {
                    resetForm();
                    setVisible(true);
                  }}
                >
                  Add Pricing Rule
                </Button>
              </SpaceBetween>
            }
          >
            Dynamic Pricing Rules & Surcharges ({rules.length})
          </Header>
        }
        empty={
          <Box textAlign="center" color="inherit">
            <b>No pricing rules found</b>
            <Box padding={{ bottom: "s" }} variant="p" color="inherit">
              Create rules to apply format surcharges, day discounts, or matinee rates.
            </Box>
          </Box>
        }
      />

      <Modal
        onDismiss={() => setVisible(false)}
        visible={visible}
        closeAriaLabel="Close modal"
        header={editingRule ? "Edit Pricing Rule" : "Add Pricing Rule"}
        footer={
          <Box float="right">
            <SpaceBetween direction="horizontal" size="xs">
              <Button variant="link" onClick={() => setVisible(false)}>
                Cancel
              </Button>
              <Button
                variant="primary"
                onClick={() => saveMutation.mutate()}
                loading={saveMutation.isPending}
              >
                Save Rule
              </Button>
            </SpaceBetween>
          </Box>
        }
      >
        <SpaceBetween size="m">
          <FormField label="Rule Name">
            <Input
              value={name}
              onChange={(e) => setName(e.detail.value)}
              placeholder="e.g. Cinema Wednesday Discount"
            />
          </FormField>

          <FormField label="Rule Type">
            <Select
              selectedOption={ruleType}
              onChange={(e) => setRuleType(e.detail.selectedOption)}
              options={RULE_TYPE_OPTIONS}
            />
          </FormField>

          {ruleType.value === "format_surcharge" && (
            <FormField
              label="Screen Type Code"
              description="Applies surcharge when a showtime plays in this screen format"
            >
              <Input
                value={screenTypeCode}
                onChange={(e) => setScreenTypeCode(e.detail.value.toUpperCase())}
                placeholder="e.g. IMAX, 3D, 4DX"
              />
            </FormField>
          )}

          {ruleType.value === "day_of_week" && (
            <FormField label="Day of Week">
              <Select
                selectedOption={dayOfWeek}
                onChange={(e) => setDayOfWeek(e.detail.selectedOption)}
                options={DAY_OF_WEEK_OPTIONS}
              />
            </FormField>
          )}

          {ruleType.value === "matinee" && (
            <SpaceBetween direction="horizontal" size="s">
              <FormField label="Start Time">
                <Input
                  value={startTime}
                  onChange={(e) => setStartTime(e.detail.value)}
                  placeholder="00:00:00"
                />
              </FormField>
              <FormField label="End Time">
                <Input
                  value={endTime}
                  onChange={(e) => setEndTime(e.detail.value)}
                  placeholder="12:00:00"
                />
              </FormField>
            </SpaceBetween>
          )}

          <FormField label="Adjustment Type">
            <Select
              selectedOption={adjustmentType}
              onChange={(e) => setAdjustmentType(e.detail.selectedOption)}
              options={ADJUSTMENT_TYPE_OPTIONS}
            />
          </FormField>

          <FormField
            label="Adjustment Value"
            description="Use positive values for surcharges (e.g. 2.00) or negative for discounts (e.g. -2.00)"
          >
            <Input
              type="number"
              step="0.01"
              value={adjustmentValue}
              onChange={(e) => setAdjustmentValue(e.detail.value)}
            />
          </FormField>

          <FormField label="Active Status">
            <Toggle checked={isActive} onChange={(e) => setIsActive(e.detail.checked)}>
              Enable Rule
            </Toggle>
          </FormField>
        </SpaceBetween>
      </Modal>
    </SpaceBetween>
  );
}

// --- Price Cards View ---
function PriceCardsView({ notifications, setNotifications }) {
  const queryClient = useQueryClient();
  const { data: rawCards, isLoading, error, refetch } = useQuery({
    queryKey: ["priceCards"],
    queryFn: () => apiClient("/api/v1/admin/pricing/price-cards"),
  });

  const cards = React.useMemo(() => unwrapList(rawCards), [rawCards]);

  const { data: rawTicketTypes } = useQuery({
    queryKey: ["ticketTypes"],
    queryFn: () => apiClient("/api/v1/admin/pricing/ticket-types"),
  });

  const ticketTypes = React.useMemo(() => unwrapList(rawTicketTypes), [rawTicketTypes]);

  const ticketTypeOptions = React.useMemo(() => {
    return ticketTypes.map((t) => ({
      label: `${t.name || t.code} (${t.code})`,
      value: t.ticketTypeId,
    }));
  }, [ticketTypes]);

  const [visible, setVisible] = React.useState(false);
  const [editingCard, setEditingCard] = React.useState(null);

  const [name, setName] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [entries, setEntries] = React.useState([]);

  const resetForm = () => {
    setEditingCard(null);
    setName("");
    setDescription("");
    // Pre-populate sensible matrix rows for each ticket type with standard seat
    const initialRows = ticketTypeOptions.map((tt) => ({
      ticketTypeId: tt,
      seatType: SEAT_TYPE_OPTIONS[0],
      price: tt.label.includes("CHILD") ? "6.00" : tt.label.includes("SENIOR") ? "6.50" : "8.50",
    }));
    setEntries(initialRows.length > 0 ? initialRows : [{}]);
  };

  const openEdit = async (card) => {
    setEditingCard(card);
    setName(card.name || "");
    setDescription(card.description || "");
    try {
      const full = await apiClient(`/api/v1/admin/pricing/price-cards/${card.priceCardId}`);
      const rawEntries = full?.entries || full?.value?.entries || [];
      const mapped = rawEntries.map((e) => {
        const foundTicketType = ticketTypeOptions.find((t) => t.value === e.ticketTypeId) || {
          label: `${e.ticketTypeName || e.ticketTypeCode} (${e.ticketTypeCode})`,
          value: e.ticketTypeId,
        };
        const foundSeat = SEAT_TYPE_OPTIONS.find((s) => s.value === e.seatType) || {
          label: e.seatType,
          value: e.seatType,
        };
        return {
          ticketTypeId: foundTicketType,
          seatType: foundSeat,
          price: e.price != null ? e.price.toString() : "0",
        };
      });
      setEntries(mapped);
    } catch (err) {
      setNotifications([
        {
          type: "error",
          content: "Failed to load price card details: " + err.message,
          dismissible: true,
          id: Date.now().toString(),
        },
      ]);
    }
    setVisible(true);
  };

  const saveMutation = useMutation({
    mutationFn: async () => {
      const payload = {
        name,
        description,
        isActive: true,
        entries: entries
          .filter((e) => e.ticketTypeId?.value && (e.seatType?.value || e.seatType))
          .map((e) => ({
            ticketTypeId: e.ticketTypeId.value,
            seatType: e.seatType?.value || e.seatType,
            price: parseFloat(e.price || "0"),
          })),
      };

      if (editingCard) {
        return apiClient(`/api/v1/admin/pricing/price-cards/${editingCard.priceCardId}`, {
          method: "PUT",
          body: JSON.stringify(payload),
        });
      } else {
        return apiClient("/api/v1/admin/pricing/price-cards", {
          method: "POST",
          body: JSON.stringify(payload),
        });
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries(["priceCards"]);
      setVisible(false);
      setNotifications([
        {
          type: "success",
          content: `Price card "${name}" saved successfully.`,
          dismissible: true,
          id: Date.now().toString(),
        },
      ]);
    },
    onError: (err) => {
      setNotifications([
        {
          type: "error",
          content: err.message || "Failed to save price card",
          dismissible: true,
          id: Date.now().toString(),
        },
      ]);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (id) =>
      apiClient(`/api/v1/admin/pricing/price-cards/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      queryClient.invalidateQueries(["priceCards"]);
      setNotifications([
        {
          type: "success",
          content: "Price card deleted.",
          dismissible: true,
          id: Date.now().toString(),
        },
      ]);
    },
    onError: (err) => {
      setNotifications([
        {
          type: "error",
          content: err.message || "Failed to delete price card",
          dismissible: true,
          id: Date.now().toString(),
        },
      ]);
    },
  });

  return (
    <SpaceBetween size="l">
      {error && <Alert type="error" header="Failed to load price cards">{error.message}</Alert>}

      <Table
        columnDefinitions={[
          {
            id: "name",
            header: "Card Name",
            cell: (e) => <span style={{ fontWeight: 600 }}>{e.name}</span>,
            isRowHeader: true,
          },
          {
            id: "desc",
            header: "Description",
            cell: (e) => e.description || "-",
          },
          {
            id: "entries",
            header: "Matrix Entries",
            cell: (e) => (
              <Badge color="blue">{e.entryCount != null ? `${e.entryCount} rates` : "-"}</Badge>
            ),
          },
          {
            id: "active",
            header: "Status",
            cell: (e) => (
              <StatusIndicator type={e.isActive ? "success" : "stopped"}>
                {e.isActive ? "Active" : "Inactive"}
              </StatusIndicator>
            ),
          },
          {
            id: "actions",
            header: "Actions",
            cell: (e) => (
              <SpaceBetween direction="horizontal" size="xs">
                <Button onClick={() => openEdit(e)}>Edit Matrix</Button>
                <Button
                  onClick={() => {
                    if (confirm(`Are you sure you want to delete "${e.name}"?`)) {
                      deleteMutation.mutate(e.priceCardId);
                    }
                  }}
                  loading={deleteMutation.isPending}
                >
                  Delete
                </Button>
              </SpaceBetween>
            ),
          },
        ]}
        items={cards}
        loading={isLoading}
        loadingText="Loading price cards..."
        header={
          <Header
            actions={
              <SpaceBetween direction="horizontal" size="xs">
                <Button onClick={() => refetch()} iconName="refresh" />
                <Button
                  variant="primary"
                  onClick={() => {
                    resetForm();
                    setVisible(true);
                  }}
                >
                  Add Price Card
                </Button>
              </SpaceBetween>
            }
          >
            Price Cards ({cards.length})
          </Header>
        }
        empty={
          <Box textAlign="center" color="inherit">
            <b>No price cards found</b>
            <Box padding={{ bottom: "s" }} variant="p" color="inherit">
              Price cards define base ticket prices by ticket type and seat type.
            </Box>
          </Box>
        }
      />

      <Modal
        onDismiss={() => setVisible(false)}
        visible={visible}
        closeAriaLabel="Close modal"
        header={editingCard ? `Edit Price Card: ${name}` : "Add New Price Card"}
        size="large"
        footer={
          <Box float="right">
            <SpaceBetween direction="horizontal" size="xs">
              <Button variant="link" onClick={() => setVisible(false)}>
                Cancel
              </Button>
              <Button
                variant="primary"
                onClick={() => saveMutation.mutate()}
                loading={saveMutation.isPending}
              >
                Save Price Card
              </Button>
            </SpaceBetween>
          </Box>
        }
      >
        <SpaceBetween size="m">
          <FormField label="Card Name" description="Descriptive title for showtime assignment">
            <Input
              value={name}
              onChange={(e) => setName(e.detail.value)}
              placeholder="e.g. Standard 2D Pricing, Premium IMAX Pricing"
            />
          </FormField>

          <FormField label="Description">
            <Input
              value={description}
              onChange={(e) => setDescription(e.detail.value)}
              placeholder="e.g. Default ticket rates for regular screenings"
            />
          </FormField>

          <FormField
            label="Pricing Matrix (Ticket Type × Seat Type → Price)"
            description="Configure price for each combination of ticket tier and auditorium seating class"
          >
            <AttributeEditor
              onAddButtonClick={() =>
                setEntries([
                  ...entries,
                  {
                    ticketTypeId: ticketTypeOptions[0] || null,
                    seatType: SEAT_TYPE_OPTIONS[0],
                    price: "8.50",
                  },
                ])
              }
              onRemoveButtonClick={({ detail: { itemIndex } }) => {
                const tmp = [...entries];
                tmp.splice(itemIndex, 1);
                setEntries(tmp);
              }}
              items={entries}
              addButtonText="Add Matrix Entry"
              removeButtonText="Remove"
              empty="No pricing matrix entries configured. Click 'Add Matrix Entry' to configure rates."
              definition={[
                {
                  label: "Ticket Type",
                  control: (item) => (
                    <Select
                      options={ticketTypeOptions}
                      selectedOption={item.ticketTypeId || null}
                      onChange={(e) => {
                        const idx = entries.indexOf(item);
                        if (idx >= 0) {
                          const tmp = [...entries];
                          tmp[idx] = { ...item, ticketTypeId: e.detail.selectedOption };
                          setEntries(tmp);
                        }
                      }}
                      placeholder="Select ticket..."
                    />
                  ),
                },
                {
                  label: "Seat Type",
                  control: (item) => (
                    <Select
                      options={SEAT_TYPE_OPTIONS}
                      selectedOption={
                        typeof item.seatType === "object"
                          ? item.seatType
                          : SEAT_TYPE_OPTIONS.find((s) => s.value === item.seatType) || {
                              label: item.seatType || "Standard",
                              value: item.seatType || "standard",
                            }
                      }
                      onChange={(e) => {
                        const idx = entries.indexOf(item);
                        if (idx >= 0) {
                          const tmp = [...entries];
                          tmp[idx] = { ...item, seatType: e.detail.selectedOption };
                          setEntries(tmp);
                        }
                      }}
                    />
                  ),
                },
                {
                  label: "Price ($)",
                  control: (item) => (
                    <Input
                      type="number"
                      step="0.01"
                      value={item.price != null ? item.price.toString() : ""}
                      onChange={(e) => {
                        const idx = entries.indexOf(item);
                        if (idx >= 0) {
                          const tmp = [...entries];
                          tmp[idx] = { ...item, price: e.detail.value };
                          setEntries(tmp);
                        }
                      }}
                      placeholder="0.00"
                    />
                  ),
                },
              ]}
            />
          </FormField>
        </SpaceBetween>
      </Modal>
    </SpaceBetween>
  );
}

// --- Main Pricing Management Page ---
function PricingContentInner() {
  const [notifications, setNotifications] = React.useState([]);

  return (
    <ContentLayout
      header={
        <Header
          variant="h1"
          description="Manage master price cards, ticket matrices, and automated dynamic surcharge rules"
        >
          Pricing & Rules Management
        </Header>
      }
    >
      <SpaceBetween size="l">
        {notifications.length > 0 && <Flashbar items={notifications} />}
        <Tabs
          tabs={[
            {
              label: "Price Cards & Matrices",
              id: "price-cards",
              content: (
                <PriceCardsView
                  notifications={notifications}
                  setNotifications={setNotifications}
                />
              ),
            },
            {
              label: "Dynamic Surcharges & Rules",
              id: "pricing-rules",
              content: (
                <PricingRulesView
                  notifications={notifications}
                  setNotifications={setNotifications}
                />
              ),
            },
          ]}
        />
      </SpaceBetween>
    </ContentLayout>
  );
}

export default function PricingContent() {
  return (
    <QueryWrapper>
      <PricingContentInner />
    </QueryWrapper>
  );
}
