export type StoryboardShot = {
  id: string;
  scene: number;
  heading: string;
  visual: string;
  dialogue?: string;
  durationSec: number;
  palette: string;
};

/** Pre-authored roles — screenplay/storyboard already cached for these. */
export type ClassicCharacter = {
  id: string;
  name: string;
  blurb: string;
  /** If true, great slot for a personal photo (kid, spouse, etc.) */
  personalizable: boolean;
};

export type ClassicSource = {
  id: string;
  title: string;
  author: string;
  year: number;
  genre: string;
  synopsis: string;
  excerpt: string;
  screenplay: string;
  shots: StoryboardShot[];
  characters: ClassicCharacter[];
  /** Cached pipeline = lower generate cost */
  cached: true;
};

export type PublicDemo = {
  id: string;
  title: string;
  genre: string;
  description: string;
  projectId: string;
  classicId: string;
  createdBy: string;
  createdAt: string;
  sizeBytes: number;
  stars: number;
  youtubeId?: string;
};

export const classics: ClassicSource[] = [
  {
    id: "tell-tale-heart",
    title: "The Tell-Tale Heart",
    author: "Edgar Allan Poe",
    year: 1843,
    genre: "Gothic horror",
    cached: true,
    synopsis:
      "A narrator insists on their sanity while describing the careful murder of an old man — and the relentless heartbeat that follows.",
    characters: [
      {
        id: "narrator",
        name: "Narrator",
        blurb: "Unreliable lead — often played by you or a parent for a personal twist.",
        personalizable: true,
      },
      {
        id: "old-man",
        name: "The old man",
        blurb: "The watched presence — keep classic or cast someone close.",
        personalizable: true,
      },
    ],
    excerpt: `TRUE! — nervous — very, very dreadfully nervous I had been and am; but why will you say that I am mad? The disease had sharpened my senses — not destroyed — not dulled them. Above all was the sense of hearing acute. I heard all things in the heaven and in the earth. I heard many things in hell. How, then, am I mad? Hearken! and observe how healthily — how calmly I can tell you the whole story.

It is impossible to say how first the idea entered my brain; but once conceived, it haunted me day and night. Object there was none. Passion there was none. I loved the old man. He had never wronged me. He had never given me insult. For his gold I had no desire. I think it was his eye! yes, it was this! He had the eye of a vulture — a pale blue eye, with a film over it. Whenever it fell upon me, my blood ran cold; and so by degrees — very gradually — I made up my mind to take the life of the old man, and thus rid myself of the eye forever.`,
    screenplay: `Title: THE TELL-TALE HEART
Credit: Adapted from
Author: Edgar Allan Poe
Draft date: 2026-07-31

FADE IN:

INT. NARROW ROOM - NIGHT

A single oil lamp. Floorboards. A pale blue eye watches from the dark.

NARRATOR (V.O.)
True! Nervous — very, very dreadfully nervous I had been and am. But why will you say that I am mad?

FADE OUT.`,
    shots: [
      {
        id: "t1",
        scene: 1,
        heading: "INT. NARROW ROOM - NIGHT",
        visual: "Oil lamp, warped floorboards, a single watching eye in shadow.",
        dialogue: "True! Nervous — very, very dreadfully nervous…",
        durationSec: 8,
        palette: "from-[#1a1410] to-[#0c0a08]",
      },
      {
        id: "t2",
        scene: 2,
        heading: "INT. HALL - NIGHT",
        visual: "A thin lantern beam under a bedroom door. Breath held.",
        dialogue: "I loved the old man. It was his eye.",
        durationSec: 6,
        palette: "from-[#16120e] to-[#0a0908]",
      },
      {
        id: "t3",
        scene: 3,
        heading: "INT. BEDROOM - NIGHT",
        visual: "Sleeping figure. A pale blue eye opens in the dark.",
        durationSec: 7,
        palette: "from-[#1c1814] to-[#0c0a08]",
      },
      {
        id: "t4",
        scene: 4,
        heading: "BLACK",
        visual: "Heartbeat in pure dark. Silence broken by a pulse.",
        dialogue: "It is the beating of his hideous heart!",
        durationSec: 5,
        palette: "from-[#0a0a0a] to-[#000000]",
      },
    ],
  },
  {
    id: "alice",
    title: "Alice's Adventures in Wonderland",
    author: "Lewis Carroll",
    year: 1865,
    genre: "Fantasy",
    cached: true,
    synopsis:
      "A curious girl tumbles into a dreamlike underland — perfect for casting a child as Alice.",
    characters: [
      {
        id: "alice",
        name: "Alice",
        blurb: "Curious lead — ideal place for a photo of your child.",
        personalizable: true,
      },
      {
        id: "rabbit",
        name: "White Rabbit",
        blurb: "The hurried guide into Wonderland.",
        personalizable: true,
      },
      {
        id: "caterpillar",
        name: "Caterpillar",
        blurb: "The slow, questioning sage on the mushroom.",
        personalizable: false,
      },
    ],
    excerpt: `Alice was beginning to get very tired of sitting by her sister on the bank, and of having nothing to do: once or twice she had peeped into the book her sister was reading, but it had no pictures or conversations in it, "and what is the use of a book," thought Alice "without pictures or conversations?"

So she was considering in her own mind whether the pleasure of making a daisy-chain would be worth the trouble of getting up and picking the daisies, when suddenly a White Rabbit with pink eyes ran close by her.

There was nothing so very remarkable in that; nor did Alice think it so very much out of the way to hear the Rabbit say to itself, "Oh dear! Oh dear! I shall be late!"`,
    screenplay: `Title: ALICE'S ADVENTURES IN WONDERLAND
Credit: Adapted from
Author: Lewis Carroll

FADE IN:

EXT. RIVERBANK - DAY

ALICE lies beside her sister. A WHITE RABBIT in a waistcoat darts past.

WHITE RABBIT
Oh dear! Oh dear! I shall be late!

Alice runs after him toward a rabbit-hole under the hedge.

FADE OUT.`,
    shots: [
      {
        id: "a1",
        scene: 1,
        heading: "EXT. RIVERBANK - DAY",
        visual: "Sun-bleached grass, a drowsy girl, a book without pictures.",
        dialogue: "What is the use of a book without pictures…",
        durationSec: 7,
        palette: "from-[#1a2218] to-[#0a100c]",
      },
      {
        id: "a2",
        scene: 2,
        heading: "EXT. FIELD - DAY",
        visual: "A white rabbit in a waistcoat checks a pocket-watch.",
        dialogue: "Oh dear! Oh dear! I shall be late!",
        durationSec: 5,
        palette: "from-[#1c2018] to-[#0c100a]",
      },
      {
        id: "a3",
        scene: 3,
        heading: "EXT. HEDGE - DAY",
        visual: "Alice at the mouth of a dark rabbit-hole, curiosity winning.",
        durationSec: 6,
        palette: "from-[#161c14] to-[#080c08]",
      },
      {
        id: "a4",
        scene: 4,
        heading: "INT. RABBIT-HOLE - CONTINUOUS",
        visual: "Slow fall past shelves, jars, and floating maps.",
        dialogue: "Down, down, down…",
        durationSec: 8,
        palette: "from-[#12161a] to-[#06080c]",
      },
    ],
  },
  {
    id: "romeo",
    title: "Romeo and Juliet",
    author: "William Shakespeare",
    year: 1597,
    genre: "Tragedy",
    cached: true,
    synopsis:
      "Star-crossed lovers meet under a Veronese moon — cast partners as the lovers for a personal short.",
    characters: [
      {
        id: "romeo",
        name: "Romeo",
        blurb: "Montague lead — cast yourself, a partner, or leave classic.",
        personalizable: true,
      },
      {
        id: "juliet",
        name: "Juliet",
        blurb: "Capulet lead — a natural spouse/partner swap.",
        personalizable: true,
      },
      {
        id: "nurse",
        name: "Nurse",
        blurb: "Warm confidante at the ball.",
        personalizable: true,
      },
    ],
    excerpt: `Two households, both alike in dignity,
In fair Verona, where we lay our scene,
From ancient grudge break to new mutiny,
Where civil blood makes civil hands unclean.
From forth the fatal loins of these two foes
A pair of star-cross'd lovers take their life;

ROMEO
O, she doth teach the torches to burn bright!

JULIET
My only love sprung from my only hate!
Too early seen unknown, and known too late!`,
    screenplay: `Title: ROMEO AND JULIET
Credit: Adapted from
Author: William Shakespeare

FADE IN:

INT. CAPULET BALL - NIGHT

ROMEO sees JULIET across the floor.

ROMEO
O, she doth teach the torches to burn bright!

JULIET
My only love sprung from my only hate!

FADE OUT.`,
    shots: [
      {
        id: "r1",
        scene: 1,
        heading: "EXT. VERONA STREET - NIGHT",
        visual: "Torch smoke, rival crests, music from a ball above.",
        dialogue: "Two households, both alike in dignity…",
        durationSec: 6,
        palette: "from-[#1a1218] to-[#0c080e]",
      },
      {
        id: "r2",
        scene: 2,
        heading: "INT. CAPULET BALL - NIGHT",
        visual: "Masks and candlelight; Romeo freezes mid-step.",
        dialogue: "O, she doth teach the torches to burn bright!",
        durationSec: 7,
        palette: "from-[#1c1410] to-[#0e0a08]",
      },
      {
        id: "r3",
        scene: 3,
        heading: "INT. CAPULET BALL - CONTINUOUS",
        visual: "Juliet turns; the room falls away to two faces.",
        dialogue: "My only love sprung from my only hate!",
        durationSec: 6,
        palette: "from-[#18121a] to-[#0a0810]",
      },
      {
        id: "r4",
        scene: 4,
        heading: "INT. BALL - LATER",
        visual: "Hands almost touch as dancers blur past.",
        durationSec: 5,
        palette: "from-[#141018] to-[#08060c]",
      },
    ],
  },
];

export const publicDemos: PublicDemo[] = [
  {
    id: "demo-tell-1",
    title: "The Tell-Tale Heart — Pulse Cut",
    genre: "Gothic horror",
    description: "A tight gothic short with heartbeat sound design cues.",
    projectId: "ptm-demo-tell-1",
    classicId: "tell-tale-heart",
    createdBy: "studio",
    createdAt: "2026-07-20T12:00:00.000Z",
    sizeBytes: 4_200_000,
    stars: 128,
    youtubeId: "2iKZmRR9RkE",
  },
  {
    id: "demo-tell-2",
    title: "The Tell-Tale Heart — Eye Study",
    genre: "Gothic horror",
    description: "Coverage study on the pale blue eye motif.",
    projectId: "ptm-demo-tell-2",
    classicId: "tell-tale-heart",
    createdBy: "studio",
    createdAt: "2026-07-22T15:00:00.000Z",
    sizeBytes: 3_800_000,
    stars: 96,
  },
  {
    id: "demo-alice",
    title: "Alice — Riverbank Cold Open",
    genre: "Fantasy",
    description: "Wonderland cold open with rabbit-hole fall — great with a child as Alice.",
    projectId: "ptm-demo-alice",
    classicId: "alice",
    createdBy: "studio",
    createdAt: "2026-07-25T10:00:00.000Z",
    sizeBytes: 5_100_000,
    stars: 210,
  },
];
