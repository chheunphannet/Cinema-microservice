import * as React from "react";
import { useState, useEffect } from "react";
import Table from "@cloudscape-design/components/table";
import Box from "@cloudscape-design/components/box";
import Header from "@cloudscape-design/components/header";
import Alert from "@cloudscape-design/components/alert";
import ContentLayout from "@cloudscape-design/components/content-layout";
import SpaceBetween from "@cloudscape-design/components/space-between";
import { apiClient } from "../lib/apiClient";

export default function MoviesTable() {
  const [movies, setMovies] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    async function fetchMovies() {
      try {
        const data = await apiClient('/api/v1/catalog/movies');
        // The API returns an array for GetMoviesAsync
        if (Array.isArray(data)) {
          setMovies(data);
        } else if (data && Array.isArray(data.items)) {
          setMovies(data.items);
        } else if (data && Array.isArray(data.data)) {
          setMovies(data.data);
        } else {
          setMovies([]);
        }
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    }
    fetchMovies();
  }, []);

  if (error) {
    return (
      <ContentLayout header={<Header variant="h1">Movies</Header>}>
        <Alert type="error" header="Failed to fetch movies">{error}</Alert>
      </ContentLayout>
    );
  }

  return (
    <ContentLayout header={<Header variant="h1">Movies</Header>}>
      <SpaceBetween size="l">
        <Table
          columnDefinitions={[
            {
              id: "id",
              header: "ID",
              cell: item => item.movieId || "-",
              isRowHeader: true
            },
            {
              id: "title",
              header: "Title",
              cell: item => item.title || "-"
            },
            {
              id: "genre",
              header: "Genre",
              cell: item => item.genre || "-"
            },
            {
              id: "duration",
              header: "Duration (min)",
              cell: item => item.durationMinutes || "-"
            },
            {
              id: "status",
              header: "Status",
              cell: item => item.releaseStatus || (item.isActive ? "Active" : "Inactive")
            }
          ]}
          items={movies}
          loading={loading}
          loadingText="Loading movies"
          empty={
            <Box textAlign="center" color="inherit">
              <b>No movies found</b>
              <Box padding={{ bottom: "s" }} variant="p" color="inherit">
                No active movies to display.
              </Box>
            </Box>
          }
          header={<Header variant="h2">Movie Catalog</Header>}
        />
      </SpaceBetween>
    </ContentLayout>
  );
}
