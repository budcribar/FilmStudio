import { createFileRoute, Link } from "@tanstack/react-router";
import { Clapperboard, FolderOpen, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { useProjects } from "@/lib/ptm/store";
import type { FilmProject } from "@/lib/ptm/types";
import { resumeActionLabel } from "@/lib/ptm/types";
import { formatRelativeTime } from "@/lib/utils";

export const Route = createFileRoute("/projects")({
  component: ProjectsPage,
});

function projectBadge(p: FilmProject) {
  if (p.status === "ready") return { label: "Movie ready", variant: "success" as const };
  if (p.status === "generating") return { label: "Rendering", variant: "cinema" as const };
  if (p.status === "sample") return { label: "Free sample", variant: "accent" as const };
  if (p.wizardStep === "cast") return { label: "Casting", variant: "default" as const };
  if (p.wizardStep === "estimate") return { label: "Estimate", variant: "cinema" as const };
  if (p.wizardStep === "confirm") return { label: "Confirm", variant: "cinema" as const };
  return { label: "Setup", variant: "default" as const };
}

function ProjectsPage() {
  const projects = useProjects((s) => s.projects);
  const deleteProject = useProjects((s) => s.deleteProject);

  return (
    <div className="mx-auto max-w-6xl px-4 sm:px-6 py-10 sm:py-14">
      <div className="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-4 mb-8">
        <div>
          <p className="text-xs font-medium uppercase tracking-[0.14em] text-fg-subtle mb-2">
            Your shelf
          </p>
          <h1 className="font-display text-3xl sm:text-4xl font-semibold tracking-tight">
            Projects
          </h1>
          <p className="mt-2 text-sm text-fg-muted">
            Book → cast → estimate → confirm → generate. Local to this browser.
          </p>
        </div>
        <Button asChild>
          <Link to="/studio">New project</Link>
        </Button>
      </div>

      {projects.length === 0 ? (
        <Card>
          <CardContent className="py-16 flex flex-col items-center text-center px-6">
            <span className="flex h-12 w-12 items-center justify-center rounded-[var(--radius-md)] border border-border bg-bg-subtle text-fg-muted mb-4">
              <FolderOpen className="h-5 w-5" />
            </span>
            <h2 className="font-display text-lg font-semibold">No projects yet</h2>
            <p className="mt-2 text-sm text-fg-muted max-w-sm">
              Pick a classic (cheaper, cached) or drop your own page, cast your people, then
              generate.
            </p>
            <Button asChild className="mt-6">
              <Link to="/studio">
                <Clapperboard className="h-4 w-4" />
                Pick a book
              </Link>
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {projects.map((p) => {
            const b = projectBadge(p);
            return (
              <Card key={p.id} className="flex flex-col">
                <CardContent className="p-5 flex flex-col flex-1">
                  <div className="flex items-start justify-between gap-2 mb-3">
                    <Badge variant={b.variant}>{b.label}</Badge>
                    <button
                      type="button"
                      aria-label={`Delete ${p.title}`}
                      className="text-fg-subtle hover:text-danger p-1"
                      onClick={() => deleteProject(p.id)}
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                  <h2 className="font-display text-lg font-semibold tracking-tight line-clamp-1">
                    {p.title}
                  </h2>
                  <p className="text-xs text-fg-muted mt-1">
                    {p.sourceKind === "classic" ? "Cached classic" : "Custom"} · {p.genre}
                    {p.estimate ? ` · ${p.estimate.creditsFull} cr` : ""}
                  </p>
                  <p className="text-xs text-fg-subtle mt-3">
                    Updated {formatRelativeTime(p.updatedAt)}
                  </p>
                  <Button asChild variant="secondary" className="mt-4 w-full">
                    <Link to="/studio/$projectId" params={{ projectId: p.id }}>
                      {resumeActionLabel(p)}
                    </Link>
                  </Button>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
