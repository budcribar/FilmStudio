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
    startClassic(classicParam);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [classicParam]);

  function startClassic(classicId: string) {
    setError("");
    setBusy(true);
    try {
      const id = createFromClassicBook(classicId);
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
        const id = createFromCustomBook(result.title, result.text);
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

  function startPaste(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    if (text.trim().length < 40) {
      setError("Need a short page (40+ characters) or pick a classic above.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      const id = createFromCustomBook(title || "Untitled", text);
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
        <p className="mt-3 text-fg-muted text-sm sm:text-base max-w-2xl leading-relaxed">
          Start from a <strong className="text-fg font-medium">pre-built classic</strong>{" "}
          (screenplay & storyboard already cached — cheaper) or drop your own PDF/text. Next
          you’ll cast characters, see an estimate, confirm, and generate.
        </p>
      </div>

      {busy && (
        <div className="mb-6 flex items-center gap-2 text-sm text-fg-muted">
          <Loader2 className="h-4 w-4 animate-spin text-cinema" />
          Opening book — character casting next…
        </div>
      )}

      {/* Classics first — preferred cheaper path */}
      <section className="mb-8">
        <div className="flex items-center gap-2 mb-4">
          <BookOpen className="h-4 w-4 text-cinema" />
          <h2 className="font-display font-semibold text-lg">Library classics</h2>
          <Badge variant="success">Cached · lower credits</Badge>
        </div>
        <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {classics.map((c) => (
            <Card key={c.id} className="flex flex-col">
              <CardContent className="p-5 flex flex-col flex-1">
                <div className="flex flex-wrap gap-2 mb-3">
                  <Badge variant="default">{c.genre}</Badge>
                  <Badge variant="cinema">{c.characters.length} characters</Badge>
                </div>
                <h3 className="font-display text-lg font-semibold tracking-tight">{c.title}</h3>
                <p className="text-sm text-fg-muted mt-1">
                  {c.author} · {c.year}
                </p>
                <p className="mt-3 text-sm text-fg-muted leading-relaxed line-clamp-3 flex-1">
                  {c.synopsis}
                </p>
                <p className="mt-3 text-xs text-fg-subtle">
                  Cast: {c.characters.map((ch) => ch.name).join(", ")}
                </p>
                <Button
                  className="mt-4 w-full"
                  disabled={busy}
                  onClick={() => startClassic(c.id)}
                >
                  <Sparkles className="h-4 w-4" />
                  Choose this book
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      </section>

      {/* Custom */}
      <section>
        <div className="flex items-center gap-2 mb-4">
          <FileUp className="h-4 w-4 text-cinema" />
          <h2 className="font-display font-semibold text-lg">Or your own book / page</h2>
          <Badge variant="default">From scratch · higher credits</Badge>
        </div>

        <Card className="mb-4 border-border-strong">
          <CardContent className="p-5 sm:p-6">
            <input
              ref={fileInputRef}
              type="file"
              className="sr-only"
              accept={SOURCE_ACCEPT}
              disabled={busy}
              onChange={(e) => void handleFiles(e.target.files)}
            />
            <div
              role="button"
              tabIndex={busy ? -1 : 0}
              onClick={() => !busy && fileInputRef.current?.click()}
              onKeyDown={(e) => {
                if (!busy && (e.key === "Enter" || e.key === " ")) {
                  e.preventDefault();
                  fileInputRef.current?.click();
                }
              }}
              onDragOver={(e) => {
                e.preventDefault();
                if (!busy) setDragOver(true);
              }}
              onDragLeave={() => setDragOver(false)}
              onDrop={(e) => {
                e.preventDefault();
                setDragOver(false);
                if (!busy) void handleFiles(e.dataTransfer.files);
              }}
              className={cn(
                "rounded-[var(--radius-xl)] border-2 border-dashed px-4 py-10 text-center cursor-pointer",
                dragOver ? "border-primary bg-primary/10" : "border-border bg-bg",
                busy && "opacity-60 pointer-events-none",
              )}
            >
              <Upload className="h-8 w-8 mx-auto text-cinema mb-3" />
              <p className="font-display font-semibold">
                {isPhone ? "Pick PDF or text from this phone" : "Drop a PDF or text file"}
              </p>
              <p className="text-sm text-fg-muted mt-1">
                Custom path builds screenplay + board from scratch
              </p>
              <Button type="button" className="mt-4" disabled={busy}>
                {isPhone ? (
                  <>
                    <Smartphone className="h-4 w-4" />
                    From phone storage
                  </>
                ) : (
                  <>
                    <FileUp className="h-4 w-4" />
                    Choose file
                  </>
                )}
              </Button>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-5 sm:p-6">
            <h3 className="font-display font-semibold mb-3">Or paste text</h3>
            <form onSubmit={startPaste} className="space-y-3">
              <Input
                placeholder="Working title"
                value={title}
                disabled={busy}
                onChange={(e) => setTitle(e.target.value)}
              />
              <Textarea
                placeholder="Paste your page or chapter…"
                className="min-h-[140px]"
                value={text}
                disabled={busy}
                onChange={(e) => setText(e.target.value)}
              />
              <Button type="submit" disabled={busy} className="w-full sm:w-auto">
                Continue with custom page
              </Button>
            </form>
          </CardContent>
        </Card>

        {error && <p className="mt-4 text-sm text-danger">{error}</p>}
      </section>
    </div>
  );
}
