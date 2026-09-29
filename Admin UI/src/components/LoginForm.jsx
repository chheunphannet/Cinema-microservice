import * as React from "react";
import Form from "@cloudscape-design/components/form";
import SpaceBetween from "@cloudscape-design/components/space-between";
import Button from "@cloudscape-design/components/button";
import Container from "@cloudscape-design/components/container";
import Header from "@cloudscape-design/components/header";
import FormField from "@cloudscape-design/components/form-field";
import Input from "@cloudscape-design/components/input";
import Alert from "@cloudscape-design/components/alert";
import { API_BASE_URL } from "../lib/apiClient";

export default function LoginForm() {
  const [email, setEmail] = React.useState("");
  const [password, setPassword] = React.useState("");
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/identity/login`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({ username: email, pinOrPassword: password })
      });
      if (!res.ok) {
        let errMessage = "Login failed. Please check your credentials.";
        try {
          const data = await res.json();
          if (data.message) errMessage = data.message;
          else if (data.detail) errMessage = data.detail;
          else if (data.title) errMessage = data.title;
        } catch (e) {}
        throw new Error(errMessage);
      }
      const data = await res.json();
      if (data.token) {
        localStorage.setItem("token", data.token);
        localStorage.setItem("user", JSON.stringify(data));
        localStorage.setItem("user_role", data.roles?.[0] || "");
        localStorage.setItem("user_branch", data.branchId || "");
        localStorage.setItem("user_display_name", data.displayName || data.username || "");
        // If user is branch-scoped, lock cinema_admin_selected_branch to their branch
        if (data.branchId && data.roles?.[0] !== "super_admin" && data.roles?.[0] !== "system_admin" && data.roles?.[0] !== "content_manager") {
          localStorage.setItem("cinema_admin_selected_branch", data.branchId);
        }
        const urlParams = new URLSearchParams(window.location.search);
        const returnUrl = urlParams.get("returnUrl") || "/";
        window.location.href = returnUrl;
      } else {
        throw new Error("No token returned");
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} style={{ width: "400px", maxWidth: "100%" }}>
      <Form
        actions={
          <Button variant="primary" loading={loading} formAction="submit">
            Login
          </Button>
        }
      >
        <Container
          header={<Header variant="h2">Cinema POS Login</Header>}
        >
          <SpaceBetween direction="vertical" size="l">
            {error && <Alert type="error">{error}</Alert>}
            <FormField label="Username">
              <Input
                value={email}
                onChange={({ detail }) => setEmail(detail.value)}
                type="text"
              />
            </FormField>
            <FormField label="Password">
              <Input
                value={password}
                onChange={({ detail }) => setPassword(detail.value)}
                type="password"
              />
            </FormField>
          </SpaceBetween>
        </Container>
      </Form>
    </form>
  );
}
