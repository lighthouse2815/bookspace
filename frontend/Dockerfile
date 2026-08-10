FROM node:22-alpine AS build
WORKDIR /app

ARG VITE_API_BASE_URL=/api
ARG VITE_OPERATOR_NAME
ARG VITE_SUPPORT_EMAIL
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
ENV VITE_OPERATOR_NAME=${VITE_OPERATOR_NAME}
ENV VITE_SUPPORT_EMAIL=${VITE_SUPPORT_EMAIL}

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
