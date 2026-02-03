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

# Create non-root user
RUN addgroup --system --gid 1001 nodejs \
 && adduser --system --uid 1001 nextjs

# Copy only what's needed for runtime
COPY --from=builder /app/node_modules ./node_modules
COPY --from=builder /app/package.json ./package.json
COPY --from=builder /app/.next ./.next
COPY --from=builder /app/public ./public
COPY --from=builder /app/next.config.ts ./next.config.ts

USER nextjs

CMD ["npm", "start"]
