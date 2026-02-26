# Stage 1: Build React UI
FROM node:20 AS client-build
WORKDIR /app

# Copy package files and install dependencies
COPY package.json package-lock.json ./
RUN npm install

# Copy source code and build
COPY . .
RUN npm run build

# Stage 2: Serve with Nginx
FROM nginx:alpine
WORKDIR /usr/share/nginx/html

# Clean default nginx files
RUN rm -rf ./*

# Copy built assets from builder stage
COPY --from=client-build /app/dist .

# Copy nginx template
COPY nginx.conf.template /etc/nginx/nginx.conf.template

# Defaults — can be overridden in docker-compose
ENV PEER_CLIENT_API_URL=http://peer-master:8080
ENV PEER_METADATA_API_URL=http://index.api:7030
ENV PEER_GATEWAY_URL=http://gateway:5170

EXPOSE 80
# Run envsubst at startup to inject env vars into nginx config, then start nginx.
# Inline avoids Windows CRLF issues with shell script files.
CMD ["sh", "-c", "envsubst '${PEER_CLIENT_API_URL} ${PEER_METADATA_API_URL} ${PEER_GATEWAY_URL}' < /etc/nginx/nginx.conf.template > /etc/nginx/conf.d/default.conf && nginx -g 'daemon off;'"]
