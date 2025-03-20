FROM node:18-alpine AS deps
WORKDIR /app

# Install dependencies
COPY package.json package-lock.json* ./
RUN npm install

# Rebuild the source code
FROM node:18-alpine AS builder
WORKDIR /app

# Create necessary directories first
RUN mkdir -p public .next

# Copy all dependencies and source files
COPY --from=deps /app/node_modules ./node_modules
COPY . .

# Set environment variables
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PATH=$PATH:/app/node_modules/.bin

# Build the application
RUN npm run build

# Production image, copy all the files and run next
FROM node:18-alpine AS runner
WORKDIR /app

# Install curl for healthcheck
RUN apk add --no-cache curl

# Create non-root user and directories
RUN addgroup --system --gid 1001 nodejs \
    && adduser --system --uid 1001 nextjs \
    && mkdir -p /app/.next /app/public \
    && chown -R nextjs:nodejs /app

# Set environment variables
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ENV PATH=$PATH:/app/node_modules/.bin

# Copy necessary files
COPY --from=builder /app/package.json ./package.json
COPY --from=builder /app/node_modules ./node_modules
COPY --from=builder /app/.next ./.next
COPY --from=builder /app/public ./public
COPY --from=builder /app/next.config.* ./

# Switch to non-root user
USER nextjs

# Expose port
EXPOSE 3000

# Start the application
CMD ["npm", "start"]

# Add health check
HEALTHCHECK --interval=30s --timeout=30s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:3000 || exit 1
