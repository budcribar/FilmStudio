import { Coins } from "lucide-react";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { CREDIT_PACKS, useWallet } from "@/lib/ptm/wallet";
import { cn } from "@/lib/utils";

export function CreditsButton({ className }: { className?: string }) {
  const credits = useWallet((s) => s.credits);
  const buyPack = useWallet((s) => s.buyPack);
  const [open, setOpen] = useState(false);
  const [justBought, setJustBought] = useState<string | null>(null);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="secondary" size="sm" className={cn("gap-1.5 tabular-nums", className)}>
          <Coins className="h-3.5 w-3.5 text-cinema" />
          {credits} credits
        </Button>
      </DialogTrigger>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Credits</DialogTitle>
          <DialogDescription>
            Full movie renders spend credits. One free sample scene per project is always
            available. This demo buys packs instantly — no real payment.
          </DialogDescription>
        </DialogHeader>
        <p className="text-sm text-fg-muted">
          Balance:{" "}
          <span className="font-semibold text-fg tabular-nums">{credits} credits</span>
        </p>
        <div className="grid gap-2">
          {CREDIT_PACKS.map((pack) => (
            <button
              key={pack.id}
              type="button"
              onClick={() => {
                buyPack(pack.id);
                setJustBought(pack.id);
              }}
              className={cn(
                "flex items-center justify-between gap-3 rounded-[var(--radius-md)] border px-4 py-3 text-left transition-colors hover:border-border-strong hover:bg-bg-subtle",
                pack.popular ? "border-cinema/40 bg-cinema/5" : "border-border",
              )}
            >
              <div>
                <div className="flex items-center gap-2">
                  <span className="font-display font-semibold text-sm">{pack.name}</span>
                  {pack.popular && <Badge variant="cinema">Popular</Badge>}
                  {justBought === pack.id && <Badge variant="success">Added</Badge>}
                </div>
                <p className="text-xs text-fg-muted mt-0.5">{pack.blurb}</p>
              </div>
              <div className="text-right shrink-0">
                <p className="font-semibold text-sm tabular-nums">{pack.credits}</p>
                <p className="text-xs text-fg-subtle">{pack.priceLabel}</p>
              </div>
            </button>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  );
}
