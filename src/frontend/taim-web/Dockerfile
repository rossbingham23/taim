FROM node:20-alpine AS build

WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .

ARG VITE_API_URL
ARG VITE_SIGNALR_URL
ENV VITE_API_URL=$VITE_API_URL
ENV VITE_SIGNALR_URL=$VITE_SIGNALR_URL

RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html

# SPA routing + reverse-proxy for /api and /hubs (SignalR WebSocket upgrade)
RUN printf '\
server {\n\
  listen 80;\n\
  root /usr/share/nginx/html;\n\
  index index.html;\n\
\n\
  location /api/ {\n\
    proxy_pass http://taim-api:8080;\n\
    proxy_set_header Host $host;\n\
    proxy_set_header X-Real-IP $remote_addr;\n\
  }\n\
\n\
  location /hubs/ {\n\
    proxy_pass http://taim-api:8080;\n\
    proxy_http_version 1.1;\n\
    proxy_set_header Upgrade $http_upgrade;\n\
    proxy_set_header Connection "upgrade";\n\
    proxy_set_header Host $host;\n\
  }\n\
\n\
  location / {\n\
    try_files $uri $uri/ /index.html;\n\
  }\n\
}\n' > /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
