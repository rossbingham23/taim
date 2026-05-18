FROM node:20-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev
COPY index.js ./
ENV PORT=3100
EXPOSE 3100
CMD ["node", "index.js"]
