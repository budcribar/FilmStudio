import { createFileRoute, Link } from "@tanstack/react-router";
import {
  ArrowRight,
  BookOpen,
  Clapperboard,
  Film,
  Mic,
  Play,
  Sparkles,
  Users,
  Wand2,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { classics } from "@/data/classics";
import { useProjects } from "@/lib/ptm/store";
import type { FilmProject } from "@/lib/ptm/types";
import { resumeActionLabel } from "@/lib/ptm/types";
import { formatRelativeTime } from "@/lib/utils";

export const Route = createFileRoute("/")({
  component: HomePage,
});

const steps = [
  {
    icon: BookOpen,
    title: "1 · Book",
    body: "Classic (cached, cheaper) or your own PDF/text from scratch.",
  },
  {
    icon: Users,
    title: "2 · Cast",
    body: "Put your child, spouse, or yourself into one or more roles.",
  },
  {
    icon: Mic,
    title: "3 · Voice (optional)",
    body: "Stock voices free — or pay extra to clone a short personal sample.",
  },
  {
    icon: Clapperboard,
    title: "4 · Estimate · confirm · film",
    body: "See the full credit quote, then sample free or generate the cut.",
  },
];

function statusLabel(p: FilmProject) {
  if (p.status === "ready") return "Movie ready";
  if (p.status === "generating") return "Rendering";
  if (p.status === "sample") return "Free sample";
  if (p.wizardStep === "cast") return "Casting";
  if (p.wizardStep === "voice") return "Voice";
  if (p.wizardStep === "estimate") return "Estimate";
  if (p.wizardStep === "confirm") return "Confirm";
  return "Setup";
}

function statusVariant(p: FilmProject): "success" | "cinema" | "default" | "accent" {
  if (p.status === "ready") return "success";
  if (p.status === "sample") return "accent";
  if (p.status === "generating") return "cinema";
  return "default";
}

function HomePage() {
  const projects = useProjects((s) => s.projects);
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    const unsub = useProjects.persist.onFinishHydration(() => setHydrated(true));
    if (useProjects.persist.hasHydrated()) setHydrated(true);
    return unsub;
  }, []);

  const recent = useMemo(
    () => [...projects].sort((a, b) => +new Date(b.updatedAt) - +new Date(a.updatedAt)),
    [projects],
  );
  const last = recent[0];
  const hasProjects = hydrated && recent.length > 0;

  return (
    <div>
      <section className="relative overflow-hidden border-b border-border">
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 opacity-60"
          style={{
            background:
              "radial-gradient(ellipse 80% 60% at 70% -10%, color-mix(in oklab, var(--color-cinema) 22%, transparent), transparent 55%)",
          }}
        />
        <div className="relative mx-auto max-w-6xl px-4 sm:px-6 pt-16 pb-20 sm:pt-24 sm:pb-28">
          <Badge variant="cinema" className="mb-5 uppercase tracking-[0.14em] text-[10px]">
            AI Film Studio
          </Badge>
          <h1 className="font-display text-[clamp(2.25rem,5vw,3.75rem)] font-semibold tracking-[-0.03em] leading-[1.08] text-balance max-w-3xl">
            Book, cast, optional voice — then the movie.
          </h1>
          <p className="mt-5 max-w-xl text-base sm:text-lg text-fg-muted leading-relaxed">
            Classics are pre-built and cheaper. Swap in your people, add a personal voice if
            you want (paid add-on), see a clear estimate, generate, edit.
          </p>
          <div className="mt-8 flex flex-col sm:flex-row gap-3">
            <Button asChild size="lg">
              <Link to="/studio">
                {hasProjects ? "New film" : "Pick a book"}
                <ArrowRight className="h-4 w-4" />
              </Link>
            </Button>
            {hasProjects && last && (
              <Button asChild variant="secondary" size="lg">
                <Link to="/studio/$projectId" params={{ projectId: last.id }}>
                  <Play className="h-4 w-4 fill-current" />
                  {resumeActionLabel(last)}
                </Link>
              </Button>
            )}
          </div>
        </div>
      </section>

      {hasProjects && last && (
        <section className="border-b border-border bg-bg-elevated/40">
          <div className="mx-auto max-w-6xl px-4 sm:px-6 py-10">
            <Card>
              <CardContent className="p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div>
                  <Badge variant={statusVariant(last)} className="mb-2">
                    {statusLabel(last)}
                  </Badge>
                  <p className="font-display text-lg font-semibold">{last.title}</p>
                  <p className="text-sm text-fg-muted">
                    {formatRelativeTime(last.updatedAt)}
                  </p>
                </div>
                <Button asChild>
                  <Link to="/studio/$projectId" params={{ projectId: last.id }}>
                    {resumeActionLabel(last)}
                  </Link>
                </Button>
              </CardContent>
            </Card>
          </div>
        </section>
      )}

      <section className="mx-auto max-w-6xl px-4 sm:px-6 py-16 sm:py-20">
        <h2 className="font-display text-2xl sm:text-3xl font-semibold tracking-tight mb-8">
          The path
        </h2>
        <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {steps.map((step) => {
            const Icon = step.icon;
            return (
              <Card key={step.title}>
                <CardContent className="p-5">
                  <span className="flex h-9 w-9 items-center justify-center rounded-[var(--radius-sm)] border border-border bg-bg-subtle text-cinema mb-4">
                    <Icon className="h-4 w-4" />
                  </span>
                  <h3 className="font-display font-semibold">{step.title}</h3>
                  <p className="mt-2 text-sm text-fg-muted leading-relaxed">{step.body}</p>
                </CardContent>
              </Card>
            );
          })}
        </div>
      </section>

      <section className="border-t border-border bg-bg-elevated/40">
        <div className="mx-auto max-w-6xl px-4 sm:px-6 py-16 sm:py-20">
          <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-4 mb-8">
            <div>
              <h2 className="font-display text-2xl sm:text-3xl font-semibold">
                Cached classics
              </h2>
              <p className="mt-2 text-sm text-fg-muted max-w-lg">
                Pre-built bones. You cast faces — and optionally voices.
              </p>
            </div>
            <Button asChild variant="outline">
              <Link to="/studio">
                Open library
                <Wand2 className="h-4 w-4" />
              </Link>
            </Button>
          </div>
          <div className="grid md:grid-cols-3 gap-4">
            {classics.map((c) => (
              <Card key={c.id}>
                <CardContent className="p-5">
                  <Badge variant="success" className="mb-3">
                    Cached
                  </Badge>
                  <h3 className="font-display text-lg font-semibold">{c.title}</h3>
                  <p className="text-sm text-fg-muted mt-1">{c.author}</p>
                  <Button asChild variant="secondary" className="mt-4 w-full">
                    <Link to="/studio" search={{ classic: c.id }}>
                      Start with this book
                      <ArrowRight className="h-4 w-4" />
                    </Link>
                  </Button>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-4 sm:px-6 py-16">
        <div className="rounded-[var(--radius-xl)] border border-border bg-bg-elevated p-8 sm:p-12">
          <div className="flex items-center gap-2 text-cinema mb-3">
            <Sparkles className="h-4 w-4" />
            <span className="text-xs font-medium uppercase tracking-[0.14em]">
              Face + voice
            </span>
          </div>
          <h2 className="font-display text-2xl sm:text-3xl font-semibold max-w-xl text-balance">
            Your kid as Alice — and her real voice on the lines.
          </h2>
          <p className="mt-3 text-fg-muted text-sm sm:text-base max-w-lg leading-relaxed">
            Voice is never required. Skip for stock reads, or pay the add-on when you want it
            to sound like home.
          </p>
          <Button asChild size="lg" className="mt-6">
            <Link to="/studio" search={{ classic: "alice" }}>
              <Film className="h-4 w-4" />
              Try Alice
            </Link>
          </Button>
        </div>
      </section>
    </div>
  );
}
