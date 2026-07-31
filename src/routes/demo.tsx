import { createFileRoute } from "@tanstack/react-router";
import { Play, Star } from "lucide-react";
import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { classics, publicDemos } from "@/data/classics";
import { formatBytes, formatRelativeTime } from "@/lib/utils";

export const Route = createFileRoute("/demo")({
  component: DemoGalleryPage,
});

type Sort = "top" | "newest";

function DemoGalleryPage() {
  const [sort, setSort] = useState<Sort>("top");
  const [stars, setStars] = useState<Record<string, number>>(() =>
    Object.fromEntries(publicDemos.map((d) => [d.id, d.stars])),
  );
  const [starred, setStarred] = useState<Record<string, boolean>>({});
  const [activeId, setActiveId] = useState<string | null>(null);

  const demos = useMemo(() => {
    const list = [...publicDemos].map((d) => ({
      ...d,
      stars: stars[d.id] ?? d.stars,
    }));
    if (sort === "top") list.sort((a, b) => b.stars - a.stars);
    else list.sort((a, b) => +new Date(b.createdAt) - +new Date(a.createdAt));
    return list;
  }, [sort, stars]);

  const active = demos.find((d) => d.id === activeId) ?? null;

  function toggleStar(id: string) {
    setStarred((s) => {
      const next = !s[id];
      setStars((prev) => ({
        ...prev,
        [id]: Math.max(0, (prev[id] ?? 0) + (next ? 1 : -1)),
      }));
      return { ...s, [id]: next };
    });
  }

  return (
    <div className="mx-auto max-w-6xl px-4 sm:px-6 py-10 sm:py-14">
      <div className="mb-8">
        <p className="text-xs font-medium uppercase tracking-[0.14em] text-cinema mb-2">
          Public gallery
        </p>
        <h1 className="font-display text-3xl sm:text-4xl font-semibold tracking-tight">
          Demo films
        </h1>
        <p className="mt-3 max-w-2xl text-fg-muted text-sm sm:text-base leading-relaxed">
          Approved shorts made with Page to Movie — open to everyone. New submissions wait for
          review before they appear here. Star films you like; ranked by most stars.
        </p>
      </div>

      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
        <p className="text-sm text-fg-subtle tabular-nums">{demos.length} films on the wall</p>
        <Tabs value={sort} onValueChange={(v) => setSort(v as Sort)}>
          <TabsList>
            <TabsTrigger value="top">Most stars</TabsTrigger>
            <TabsTrigger value="newest">Newest</TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      <div className="grid sm:grid-cols-2 gap-4">
        {demos.map((demo) => (
          <Card key={demo.id} className="overflow-hidden group">
            <button
              type="button"
              onClick={() => setActiveId(demo.id)}
              className="relative w-full aspect-video bg-bg-subtle text-left overflow-hidden"
            >
              {demo.youtubeId ? (
                <img
                  src={`https://i.ytimg.com/vi/${demo.youtubeId}/hqdefault.jpg`}
                  alt=""
                  className="absolute inset-0 h-full w-full object-cover opacity-80 transition-opacity group-hover:opacity-95"
                  crossOrigin="anonymous"
                />
              ) : (
                <div className="absolute inset-0 bg-gradient-to-br from-bg-hover to-bg flex items-center justify-center">
                  <FilmPosterMark title={demo.title} />
                </div>
              )}
              <div className="absolute inset-0 bg-gradient-to-t from-bg/90 via-bg/20 to-transparent" />
              <span className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 flex h-14 w-14 items-center justify-center rounded-full border border-border-strong bg-bg/70 text-fg backdrop-blur-sm transition-transform group-hover:scale-105">
                <Play className="h-6 w-6 fill-current ml-0.5" />
              </span>
              <div className="absolute bottom-0 left-0 right-0 p-4">
                <Badge variant="cinema" className="mb-2">
                  {demo.genre}
                </Badge>
                <p className="font-display text-lg font-semibold tracking-tight">{demo.title}</p>
                <p className="text-xs text-fg-muted mt-0.5 font-mono">{demo.projectId}</p>
              </div>
            </button>
            <CardContent className="p-4 space-y-3">
              <p className="text-sm text-fg-muted leading-relaxed line-clamp-2">
                {demo.description}
              </p>
              <div className="flex items-center justify-between gap-3 text-xs text-fg-subtle">
                <span>
                  @{demo.createdBy} · {formatRelativeTime(demo.createdAt)} ·{" "}
                  {formatBytes(demo.sizeBytes)}
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  className={starred[demo.id] ? "text-accent" : ""}
                  onClick={() => toggleStar(demo.id)}
                >
                  <Star className={`h-3.5 w-3.5 ${starred[demo.id] ? "fill-current" : ""}`} />
                  <span className="tabular-nums">{demo.stars}</span>
                </Button>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <Dialog open={!!active} onOpenChange={(o) => !o && setActiveId(null)}>
        <DialogContent className="sm:max-w-3xl p-0 overflow-hidden gap-0">
          {active && (
            <>
              <div className="aspect-video bg-black">
                {active.youtubeId ? (
                  <iframe
                    title={active.title}
                    src={`https://www.youtube.com/embed/${active.youtubeId}?autoplay=1&rel=0`}
                    className="h-full w-full border-0"
                    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                    allowFullScreen
                  />
                ) : (
                  <StoryboardPlayer
                    classicId={"classicId" in active ? (active as { classicId?: string }).classicId : undefined}
                    title={active.title}
                  />
                )}
              </div>
              <div className="p-6 space-y-3">
                <DialogHeader>
                  <DialogTitle>{active.title}</DialogTitle>
                  <DialogDescription>{active.description}</DialogDescription>
                </DialogHeader>
                <div className="flex flex-wrap items-center gap-3 text-xs text-fg-subtle">
                  <span className="font-mono">{active.projectId}</span>
                  <span>·</span>
                  <span>Shared by @{active.createdBy}</span>
                  <span>·</span>
                  <span>{formatRelativeTime(active.createdAt)}</span>
                </div>
              </div>
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function FilmPosterMark({ title }: { title: string }) {
  return (
    <div className="text-center px-6">
      <p className="font-display text-xl font-semibold tracking-tight text-fg/90">{title}</p>
      <p className="text-xs text-fg-subtle mt-2 uppercase tracking-[0.16em]">In-studio cut</p>
    </div>
  );
}

function StoryboardPlayer({
  classicId,
  title,
}: {
  classicId?: string;
  title: string;
}) {
  const classic = classics.find((c) => c.id === classicId) ?? classics[1];
  const [i, setI] = useState(0);
  const shot = classic?.shots[i];

  if (!shot || !classic) {
    return (
      <div className="h-full flex items-center justify-center text-fg-muted text-sm">
        No preview available
      </div>
    );
  }

  return (
    <div className={`h-full w-full bg-gradient-to-br ${shot.palette} flex flex-col`}>
      <div className="flex-1 flex flex-col items-center justify-center p-8 text-center">
        <p className="text-xs uppercase tracking-[0.16em] text-fg-subtle mb-3">
          {title} · Shot {i + 1}/{classic.shots.length}
        </p>
        <p className="font-mono text-xs text-cinema mb-2">{shot.heading}</p>
        <p className="font-display text-lg sm:text-xl font-medium max-w-md text-balance">
          {shot.visual}
        </p>
        {shot.dialogue && (
          <p className="mt-4 text-sm text-fg-muted italic max-w-sm">“{shot.dialogue}”</p>
        )}
      </div>
      <div className="flex items-center justify-between gap-2 p-4 border-t border-white/10">
        <Button
          size="sm"
          variant="secondary"
          disabled={i === 0}
          onClick={() => setI((v) => Math.max(0, v - 1))}
        >
          Prev
        </Button>
        <div className="flex gap-1.5">
          {classic.shots.map((s, idx) => (
            <button
              key={s.id}
              type="button"
              aria-label={`Shot ${idx + 1}`}
              onClick={() => setI(idx)}
              className={`h-1.5 w-6 rounded-full transition-colors ${
                idx === i ? "bg-accent" : "bg-white/20"
              }`}
            />
          ))}
        </div>
        <Button
          size="sm"
          variant="secondary"
          disabled={i >= classic.shots.length - 1}
          onClick={() => setI((v) => Math.min(classic.shots.length - 1, v + 1))}
        >
          Next
        </Button>
      </div>
    </div>
  );
}
