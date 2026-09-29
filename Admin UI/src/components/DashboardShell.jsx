import * as React from "react";
import AppLayout from "@cloudscape-design/components/app-layout";
import TopNav from "./TopNav";
import SideNav from "./SideNav";
import { I18nProvider } from "@cloudscape-design/components/i18n";
import messages from "@cloudscape-design/components/i18n/messages/all.en";
import { useAuth } from "../hooks/useAuth";

export default function DashboardShell({ children, contentType = "default" }) {
  const [navigationOpen, setNavigationOpen] = React.useState(true);
  const { isAuthenticated } = useAuth();
  const [isClient, setIsClient] = React.useState(false);

  React.useEffect(() => {
    setIsClient(true);
    if (!isAuthenticated && window.location.pathname !== "/login") {
      window.location.href = "/login?returnUrl=" + encodeURIComponent(window.location.pathname);
    }
  }, [isAuthenticated]);

  if (!isClient || !isAuthenticated) return null; // Prevent hydration flash

  return (
    <I18nProvider locale="en" messages={[messages]}>
      <div id="h" style={{ position: 'sticky', top: 0, zIndex: 1002 }}>
        <TopNav />
      </div>
      <AppLayout
        contentType={contentType}
        ariaLabels={{
          navigation: "Side navigation",
          navigationClose: "Close side navigation",
          navigationToggle: "Open side navigation",
        }}
        navigation={<SideNav />}
        navigationOpen={navigationOpen}
        onNavigationChange={({ detail }) => setNavigationOpen(detail.open)}
        content={children}
        toolsHide={true}
        headerSelector="#h"
      />
    </I18nProvider>
  );
}
