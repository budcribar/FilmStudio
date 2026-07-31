import { create } from "zustand";
import { persist } from "zustand/middleware";

export type CreditPack = {
  id: string;
  name: string;
  credits: number;
  priceLabel: string;
  blurb: string;
  popular?: boolean;
};

/** Demo packs — no real payment in this build. */
export const CREDIT_PACKS: CreditPack[] = [
  {
    id: "starter",
    name: "Starter",
    credits: 25,
    priceLabel: "$5",
    blurb: "Enough for one short full cut",
  },
  {
    id: "studio",
    name: "Studio",
    credits: 80,
    priceLabel: "$12",
    blurb: "A few shorts + re-renders",
    popular: true,
  },
  {
    id: "slate",
    name: "Slate",
    credits: 200,
    priceLabel: "$25",
    blurb: "For a weekend of adaptations",
  },
];

type Wallet = {
  credits: number;
  /** Demo: user has claimed free sample on project ids */
  freeSamplesUsed: string[];
  buyPack: (packId: string) => boolean;
  spend: (amount: number) => boolean;
  markFreeSample: (projectId: string) => void;
  canUseFreeSample: (projectId: string) => boolean;
};

export const useWallet = create<Wallet>()(
  persist(
    (set, get) => ({
      // New accounts start empty — free sample still works without credits
      credits: 0,
      freeSamplesUsed: [],

      buyPack: (packId) => {
        const pack = CREDIT_PACKS.find((p) => p.id === packId);
        if (!pack) return false;
        set((s) => ({ credits: s.credits + pack.credits }));
        return true;
      },

      spend: (amount) => {
        if (amount <= 0) return true;
        if (get().credits < amount) return false;
        set((s) => ({ credits: s.credits - amount }));
        return true;
      },

      markFreeSample: (projectId) => {
        if (get().freeSamplesUsed.includes(projectId)) return;
        set((s) => ({ freeSamplesUsed: [...s.freeSamplesUsed, projectId] }));
      },

      canUseFreeSample: (projectId) => !get().freeSamplesUsed.includes(projectId),
    }),
    { name: "page-to-movie-wallet" },
  ),
);
