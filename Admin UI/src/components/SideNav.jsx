import * as React from "react";
import SideNavigation from "@cloudscape-design/components/side-navigation";
import { useAuth } from "../hooks/useAuth";

export default function SideNav() {
  const { isAuthenticated, hasPermission } = useAuth();
  
  const [activeHref, setActiveHref] = React.useState(() => {
    if (typeof window !== "undefined") {
      const pathname = window.location.pathname;
      if (pathname.startsWith("/movies")) return "/movies";
      if (pathname.startsWith("/showtimes")) return "/showtimes";
      if (pathname.startsWith("/pricing")) return "/pricing";
      if (pathname.startsWith("/inventory")) return "/inventory";
      if (pathname.startsWith("/reservations")) return "/reservations";
      if (pathname.startsWith("/users")) return "/users";
      if (pathname === "/" || pathname === "") return "/";
      return pathname;
    }
    return "/";
  });

  React.useEffect(() => {
    if (typeof window !== "undefined") {
      const pathname = window.location.pathname;
      if (pathname.startsWith("/movies")) setActiveHref("/movies");
      else if (pathname.startsWith("/showtimes")) setActiveHref("/showtimes");
      else if (pathname.startsWith("/pricing")) setActiveHref("/pricing");
      else if (pathname.startsWith("/inventory")) setActiveHref("/inventory");
      else if (pathname.startsWith("/reservations")) setActiveHref("/reservations");
      else if (pathname.startsWith("/users")) setActiveHref("/users");
      else if (pathname === "/" || pathname === "") setActiveHref("/");
      else setActiveHref(pathname);
    }
  }, []);

  const navItems = [
    { type: "link", text: "Dashboard", href: "/" }
  ];

  if (!isAuthenticated) {
    return (
      <SideNavigation
        activeHref={activeHref}
        header={{ href: "/", text: "Cinema Admin" }}
        items={navItems}
      />
    );
  }

  const catalogItems = [];
  if (hasPermission("movies.read")) catalogItems.push({ type: "link", text: "Movies", href: "/movies" });
  if (hasPermission("pricing.read")) catalogItems.push({ type: "link", text: "Pricing", href: "/pricing" });
  
  if (catalogItems.length > 0) {
    navItems.push({
      type: "section",
      text: "Catalog & Content",
      items: catalogItems
    });
  }

  const opsItems = [];
  if (hasPermission("showtimes.read")) opsItems.push({ type: "link", text: "Showtimes", href: "/showtimes" });
  if (hasPermission("inventory.read")) opsItems.push({ type: "link", text: "F&B Inventory", href: "/inventory" });
  
  if (opsItems.length > 0) {
    navItems.push({
      type: "section",
      text: "Scheduling & Ops",
      items: opsItems
    });
  }

  const supportItems = [];
  if (hasPermission("bookings.read")) supportItems.push({ type: "link", text: "Reservations", href: "/reservations" });
  
  if (supportItems.length > 0) {
    navItems.push({
      type: "section",
      text: "Support & Transact",
      items: supportItems
    });
  }

  const systemItems = [];
  if (hasPermission("staff.read")) systemItems.push({ type: "link", text: "Operations & Audit", href: "/users" });
  
  if (systemItems.length > 0) {
    navItems.push({
      type: "section",
      text: "System Administration",
      items: systemItems
    });
  }

  return (
    <SideNavigation
      activeHref={activeHref}
      header={{ href: "/", text: "Cinema Admin" }}
      onFollow={event => {
        if (!event.detail.external) {
          event.preventDefault();
          setActiveHref(event.detail.href);
          window.location.href = event.detail.href;
        }
      }}
      items={navItems}
    />
  );
}
