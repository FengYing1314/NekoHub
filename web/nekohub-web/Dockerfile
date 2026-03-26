FROM node:22-alpine AS build
WORKDIR /app

ARG VITE_API_BASE_URL=/
ARG VITE_MAX_UPLOAD_SIZE_BYTES=10485760
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_MAX_UPLOAD_SIZE_BYTES=$VITE_MAX_UPLOAD_SIZE_BYTES

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.29-alpine AS runtime
WORKDIR /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist .

EXPOSE 8080
