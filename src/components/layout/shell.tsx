import { Link, useRouterState } from "@tanstack/react-router";
import { Clapperboard, Film, FolderOpen, Home, Menu, X } from "lucide-react";
import { useState } from "react";
import { CreditsButton } from "@/components/credits-dialog";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const nav = [
  { to: "/", label: "Home", icon: Home },
  { to: "/demo", label: "Demo", icon: Film },
  { to: "/projects", label: "Projects", icon: FolderOpen },
  { to: "/studio", label: "Studio", icon: Clapperboard },
] as const;

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  const [open, setOpen] = useState(false);

  return (
    <div className="min-h-dvh flex flex-col bg-bg text-fg">
      <header className="sticky top-0 z-40 border-b border-border/80 bg-bg/90 backdrop-blur-md">
        <div className="mx-auto flex h-14 max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
          <Link to="/" className="flex items-center gap-2.5 shrink-0 group">
            <span className="flex h-8 w-8 items-center justify-center rounded-[var(--radius-sm)] border border-border bg-bg-elevated text-cinema group-hover:border-border-strong transition-colors">
              <Clapperboard className="h-4 w-4" />
            </span>
            <span className="font-display text-[15px] font-semibold tracking-tight">
              Page<span className="text-fg-muted font-medium"> to </span>Movie
            </span>
          </Link>

          <nav className="hidden md:flex items-center gap-1">
            {nav.map((item) => {
              const active =
                item.to === "/"
                  ? pathname === "/"
                  : pathname === item.to || pathname.startsWith(`${item.to}/`);
              const Icon = item.icon;
              return (
                <Link
                  key={item.to}
                  to={item.to}
                  className={cn(
                    "inline-flex items-center gap-1.5 rounded-[var(--radius-sm)] px-3 py-2 text-sm font-medium transition-colors",
                    active
                      ? "bg-bg-subtle text-fg"
                      : "text-fg-muted hover:text-fg hover:bg-bg-subtle/70",
                  )}
                >
                  <Icon className="h-3.5 w-3.5 opacity-70" />
                  {item.label}
                </Link>
              );
            })}
          </nav>

          <div className="flex items-center gap-2">
            <CreditsButton className="hidden xs:inline-flex sm:inline-flex" />
            <Button asChild size="sm" className="hidden sm:inline-flex">
              <Link to="/studio">New film</Link>
            </Button>
            <Button
              variant="ghost"
              size="icon-sm"
              className="md:hidden"
              aria-label={open ? "Close menu" : "Open menu"}
              onClick={() => setOpen((v) => !v)}
            >
              {open ? <X className="h-4 w-4" /> : <Menu className="h-4 w-4" />}
            </Button>
          </div>
        </div>

        {open && (
          <div className="md:hidden border-t border-border bg-bg-elevated px-4 py-3 space-y-1">
            <div className="px-1 pb-2">
              <CreditsButton className="w-full justify-center" />
            </div>
            {nav.map((item) => {
              const Icon = item.icon;
              return (
                <Link
                  key={item.to}
                  to={item.to}
                  onClick={() => setOpen(false)}
                  className="flex items-center gap-2 rounded-[var(--radius-sm)] px-3 py-2.5 text-sm text-fg-muted hover:bg-bg-subtle hover:text-fg"
                >
                  <Icon className="h-4 w-4" />
                  {item.label}
                </Link>
              );
            })}
            <Button asChild className="w-full mt-2" onClick={() => setOpen(false)}>
              <Link to="/studio">New film</Link>
            </Button>
          </div>
        )}
      </header>

      <main className="flex-1">{children}</main>

      <footer className="border-t border-border/80 py-8 mt-auto">
        <div className="mx-auto max-w-6xl px-4 sm:px-6 flex flex-col sm:flex-row gap-3 sm:items-center sm:justify-between text-sm text-fg-subtle">
          <p className="font-display text-fg-muted">Page to Movie — AI Film Studio</p>
          <p>Drop a page · estimate · free sample or full cut with credits.</p>
        </div>
      </footer>
    </div>
  );
}
