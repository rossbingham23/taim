FROM node:20-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev
COPY index.js ./
ENV PORT=3101
EXPOSE 3101
CMD ["node", "index.js"]
