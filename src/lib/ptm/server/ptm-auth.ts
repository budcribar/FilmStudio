/**
 * PTM auth for server functions.
 *
 * - Signed-in user → their id (always preferred).
 * - Preview / no Neon (`DATABASE_URL` unset) → shared `dev-user` so the wizard
 *   works without a Grok sign-in (still writes to server PGLite tables).
 * - Deployed Neon without session → Unauthorized (fail closed).
 */
import { createMiddleware } from "@tanstack/react-start";

export const ptmAuthMiddleware = createMiddleware({ type: "function" })
  .client(async ({ next }) => {
    const { getBearerToken } = await import("@/lib/auth/client");
    return next({ sendContext: { bearerToken: getBearerToken() ?? undefined } });
  })
  .server(async ({ next, context }) => {
    const { assertSameSiteRequest } = await import("@/lib/auth/isolation.server");
    const {
      DEV_USER_ID,
      UnauthorizedError,
      getSessionUser,
      authConfigured,
    } = await import("@/lib/auth/verify.server");
    assertSameSiteRequest();

    const databaseConfigured = Boolean(process.env.DATABASE_URL?.trim());

    if (!authConfigured) {
      if (databaseConfigured) {
        throw new Error(
          "Auth disabled but DATABASE_URL set — refusing shared dev user on real DB.",
        );
      }
      return next({ context: { userId: DEV_USER_ID } });
    }

    const user = await getSessionUser(context.bearerToken);
    if (user) return next({ context: { userId: user.id } });

    // Live preview PGLite: allow anonymous project work under dev-user
    if (!databaseConfigured) {
      return next({ context: { userId: DEV_USER_ID } });
    }

    throw new UnauthorizedError();
  });
