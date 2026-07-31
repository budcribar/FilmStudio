import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { BookOpen, FileUp, Loader2, Smartphone, Sparkles, Upload } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { z } from "zod";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { classics } from "@/data/classics";
import { extractSourceFromFile, SOURCE_ACCEPT } from "@/lib/ptm/extract-source";
import { useProjects } from "@/lib/ptm/store";
import { cn } from "@/lib/utils";

const searchSchema = z.object({
  classic: z.string().optional(),
});

export const Route = createFileRoute("/studio/")({
  validateSearch: searchSchema,
  component: StudioIndexPage,
});

function useIsPhoneViewport() {
  const [isPhone, setIsPhone] = useState(false);
  useEffect(() => {
    const mq = window.matchMedia("(max-width: 640px), (hover: none) and (pointer: coarse)");
    const apply = () => setIsPhone(mq.matches);
    apply();
    mq.addEventListener("change", apply);
    return () => mq.removeEventListener("change", apply);
  }, []);
  return isPhone;
}

function StudioIndexPage() {
  const { classic: classicParam } = Route.useSearch();
  const navigate = useNavigate();
  const createFromClassicBook = useProjects((s) => s.createFromClassicBook);
  const createFromCustomBook = useProjects((s) => s.createFromCustomBook);
  const handledClassic = useRef<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const isPhone = useIsPhoneViewport();

  const [title, setTitle] = useState("");
  const [text, setText] = useState("");
  const [error, setError] = useState("");
  const [dragOver, setDragOver] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!classicParam) return;
    if (!classics.some((c) => c.id === classicParam)) return;
    if (handledClassic.current === classicParam) return;
    handledClassic.current = classicParam;
    void startClassic(classicParam);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [classicParam]);

  async function startClassic(classicId: string) {
    setError("");
    setBusy(true);
    try {
      const id = await createFromClassicBook(classicId);
      void navigate({ to: "/studio/$projectId", params: { projectId: id } });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not open that book.");
      setBusy(false);
    }
  }

  const handleFiles = useCallback(
    async (files: FileList | File[] | null) => {
      const file = files?.[0];
      if (!file || busy) return;
      setBusy(true);
      setError("");
      try {
        const result = await extractSourceFromFile(file);
        setTitle(result.title);
        setText(result.text);
        const id = await createFromCustomBook(result.title, result.text);
        void navigate({ to: "/studio/$projectId", params: { projectId: id } });
      } catch (e) {
        setError(e instanceof Error ? e.message : "Could not read that file.");
        setBusy(false);
      } finally {
        if (fileInputRef.current) fileInputRef.current.value = "";
      }
    },
    [busy, createFromCustomBook, navigate],
  );

  async function startPaste(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    if (text.trim().length < 40) {
      setError("Need a short page (40+ characters) or pick a classic above.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      const id = await createFromCustomBook(title || "Untitled", text);
      void navigate({ to: "/studio/$projectId", params: { projectId: id } });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not start.");
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto max-w-6xl px-4 sm:px-6 py-10 sm:py-14">
      <div className="mb-8 sm:mb-10">
        <Badge variant="cinema" className="mb-3 uppercase tracking-[0.14em] text-[10px]">
          Step 1 · Book
        </Badge>
        <h1 className="font-display text-3xl sm:text-4xl font-semibold tracking-tight text-balance">
          Pick a book
        </h1>
        <p className="mt-2 text-sm sm:text-base text-fg-muted max-w-2xl leading-relaxed">
          Start from a pre-built classic (screenplay & storyboard already cached — cheaper) or
          drop your own PDF/text. Project metadata saves to the server; media stays on your
          device.
        </p>
      </div>

      {error && (
        <div className="mb-6 rounded-[var(--radius-md)] border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger">
          {error}
        </div>
      )}

      {busy && (
        <div className="mb-6 flex items-center gap-2 text-sm text-fg-muted">
          <Loader2 className="h-4 w-4 animate-spin text-cinema" />
          Opening book — saving project…
        </div>
      )}

      <section className="mb-12">
        <div className="flex items-center gap-2 mb-4">
          <Sparkles className="h-4 w-4 text-cinema" />
          <h2 className="font-display text-lg font-semibold">Library classics</h2>
          <Badge variant="success" className="ml-1">
            Cached · lower credits
          </Badge>
        </div>
        <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {classics.map((c) => (
            <Card key={c.id} className="flex flex-col">
              <CardContent className="p-5 flex flex-col flex-1">
                <div className="flex items-start justify-between gap-2 mb-3">
                  <Badge variant="default">{c.genre}</Badge>
                  <span className="text-[11px] text-fg-subtle">
                    {c.characters.length} characters
                  </span>
                </div>
                <h3 className="font-display text-lg font-semibold leading-snug">{c.title}</h3>
                <p className="text-sm text-fg-muted mt-1">
                  {c.author} · {c.year}
                </p>
                <p className="text-sm text-fg-subtle mt-3 line-clamp-3 flex-1 leading-relaxed">
                  {c.synopsis}
                </p>
                <Button
                  className="mt-4 w-full"
                  disabled={busy}
                  onClick={() => void startClassic(c.id)}
                >
                  <BookOpen className="h-4 w-4" />
                  Choose this book
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      </section>

      <section>
        <div className="flex items-center gap-2 mb-4">
          <Upload className="h-4 w-4 text-cinema" />
          <h2 className="font-display text-lg font-semibold">Your own page</h2>
        </div>
        <Card
          className={cn(
            "border-dashed transition-colors",
            dragOver && "border-cinema/50 bg-cinema/5",
          )}
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragOver(false);
            void handleFiles(e.dataTransfer.files);
          }}
        >
          <CardContent className="p-5 sm:p-6 space-y-4">
            <div className="flex flex-col sm:flex-row gap-3 sm:items-center sm:justify-between">
              <div>
                <p className="font-display font-semibold">Drop PDF or text</p>
                <p className="text-sm text-fg-muted mt-1">
                  {isPhone ? (
                    <span className="inline-flex items-center gap-1.5">
                      <Smartphone className="h-3.5 w-3.5" />
                      From phone storage
                    </span>
                  ) : (
                    "From your computer — or paste below"
                  )}
                </p>
              </div>
              <Button
                type="button"
                variant="secondary"
                disabled={busy}
                onClick={() => fileInputRef.current?.click()}
              >
                <FileUp className="h-4 w-4" />
                {isPhone ? "From phone storage" : "Choose file"}
              </Button>
              <input
                ref={fileInputRef}
                type="file"
                accept={SOURCE_ACCEPT}
                className="sr-only"
                onChange={(e) => void handleFiles(e.target.files)}
              />
            </div>
            <form onSubmit={(e) => void startPaste(e)} className="space-y-3">
              <Input
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="Title (optional)"
                disabled={busy}
              />
              <Textarea
                value={text}
                onChange={(e) => setText(e.target.value)}
                placeholder="Or paste text…"
                rows={6}
                disabled={busy}
              />
              <Button type="submit" disabled={busy || text.trim().length < 40}>
                Start custom adaptation
              </Button>
            </form>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
