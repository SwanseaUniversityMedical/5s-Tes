# 1. Builder
ARG BUILDPLATFORM
FROM --platform=$BUILDPLATFORM node:25.5.0-alpine AS builder
WORKDIR /app

# Install deps
COPY package.json package-lock.json* ./
RUN npm ci

# Copy sources & build Next.js
COPY . .
RUN npm run build

# 2. Runner (multi-arch)
FROM node:25.5.0-alpine AS runner
WORKDIR /app

ENV NODE_ENV=production

# Create non-root user
RUN addgroup --system --gid 1001 nodejs \
 && adduser --system --uid 1001 nextjs

# Copy only what's needed for runtime
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static
COPY --from=builder /app/public ./public

# Change ownership of all files to nextjs user
# This allows Next.js to create directories like next-swc-fallback at runtime
RUN chown -R nextjs:nodejs /app

USER nextjs

EXPOSE 3000
CMD ["node", "server.js"]
