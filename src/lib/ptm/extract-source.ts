import pdfWorkerSrc from "pdfjs-dist/build/pdf.worker.min.mjs?url";

export type SourceImportKind = "pdf" | "text" | "markdown" | "unknown";

export type SourceImportResult = {
  text: string;
  title: string;
  fileName: string;
  kind: SourceImportKind;
  pageCount?: number;
};

const TEXT_EXT = new Set([
  "txt",
  "text",
  "md",
  "markdown",
  "fountain",
  "fdx",
  "rtf",
  "csv",
  "json",
  "html",
  "htm",
]);

function extOf(name: string) {
  const i = name.lastIndexOf(".");
  return i >= 0 ? name.slice(i + 1).toLowerCase() : "";
}

function titleFromFileName(name: string) {
  const base = name.replace(/\.[^.]+$/, "").replace(/[_-]+/g, " ").trim();
  return base || "Untitled Adaptation";
}

function kindFromFile(file: File): SourceImportKind {
  const ext = extOf(file.name);
  if (file.type === "application/pdf" || ext === "pdf") return "pdf";
  if (ext === "md" || ext === "markdown") return "markdown";
  if (TEXT_EXT.has(ext) || file.type.startsWith("text/") || file.type === "application/json") {
    return "text";
  }
  return "unknown";
}

async function extractPdfText(file: File): Promise<{ text: string; pageCount: number }> {
  const pdfjs = await import("pdfjs-dist");
  pdfjs.GlobalWorkerOptions.workerSrc = pdfWorkerSrc;

  const data = new Uint8Array(await file.arrayBuffer());
  const doc = await pdfjs.getDocument({ data }).promise;
  const parts: string[] = [];
  const maxPages = Math.min(doc.numPages, 40);

  for (let i = 1; i <= maxPages; i++) {
    const page = await doc.getPage(i);
    const content = await page.getTextContent();
    const line = content.items
      .map((item) => ("str" in item ? String(item.str) : ""))
      .join(" ")
      .replace(/\s+/g, " ")
      .trim();
    if (line) parts.push(line);
  }

  if (doc.numPages > maxPages) {
    parts.push(`\n[…truncated after ${maxPages} of ${doc.numPages} pages for this demo]`);
  }

  return { text: parts.join("\n\n").trim(), pageCount: doc.numPages };
}

function stripHtml(html: string) {
  return html
    .replace(/<script[\s\S]*?<\/script>/gi, " ")
    .replace(/<style[\s\S]*?<\/style>/gi, " ")
    .replace(/<[^>]+>/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function stripRtf(rtf: string) {
  return rtf
    .replace(/\\par[d]?/g, "\n")
    .replace(/\\'[0-9a-fA-F]{2}/g, " ")
    .replace(/\\[a-zA-Z]+-?\d* ?/g, "")
    .replace(/[{}]/g, "")
    .replace(/\s+\n/g, "\n")
    .replace(/[ \t]{2,}/g, " ")
    .trim();
}

export async function extractSourceFromFile(file: File): Promise<SourceImportResult> {
  const kind = kindFromFile(file);
  const title = titleFromFileName(file.name);

  if (kind === "pdf") {
    const { text, pageCount } = await extractPdfText(file);
    if (text.length < 20) {
      throw new Error(
        "Couldn’t extract readable text from this PDF. Try a text-based PDF, or paste the content.",
      );
    }
    return { text, title, fileName: file.name, kind, pageCount };
  }

  if (kind === "unknown") {
    const raw = await file.text();
    if (raw.includes("%PDF")) {
      const { text, pageCount } = await extractPdfText(file);
      return { text, title, fileName: file.name, kind: "pdf", pageCount };
    }
    const cleaned = raw.trim();
    if (cleaned.length < 20) {
      throw new Error(
        "Unsupported file type. Use PDF, .txt, .md, or another plain-text document.",
      );
    }
    return { text: cleaned, title, fileName: file.name, kind: "text" };
  }

  let raw = await file.text();
  const ext = extOf(file.name);
  if (ext === "html" || ext === "htm") raw = stripHtml(raw);
  if (ext === "rtf") raw = stripRtf(raw);

  const text = raw.trim();
  if (text.length < 20) {
    throw new Error("That file looks empty. Pick another document or paste text below.");
  }

  return { text, title, fileName: file.name, kind };
}

export const SOURCE_ACCEPT =
  ".pdf,.txt,.text,.md,.markdown,.fountain,.rtf,.html,.htm,text/plain,application/pdf";
