# --------- Stage 1: Build Angular app ----------
FROM node:20-alpine AS build

WORKDIR /app

# Copy dependencies first (better caching)
COPY package*.json ./
RUN npm install

# Copy the rest of the project
COPY . .

# Build the Angular app (production)
RUN npm run build

# --------- Stage 2: Serve with Nginx ----------
FROM nginx:alpine

# Copy built Angular files to nginx
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

# Copy routing configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose port
EXPOSE 80

# Start nginx
CMD ["nginx", "-g", "daemon off;"]