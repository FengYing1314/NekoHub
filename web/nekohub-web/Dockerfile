FROM node:22-alpine
WORKDIR /app

ARG VITE_API_BASE_URL=http://localhost:5121
ARG VITE_MAX_UPLOAD_SIZE_BYTES=10485760
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_MAX_UPLOAD_SIZE_BYTES=$VITE_MAX_UPLOAD_SIZE_BYTES

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

EXPOSE 4173
CMD ["npm", "run", "preview", "--", "--host", "0.0.0.0", "--port", "4173"]
