#!/bin/bash

# Update and install Docker
sudo apt-get update
sudo apt-get install -y docker.io docker-compose git
sudo systemctl enable docker
sudo systemctl start docker

# Pull your images (replace with your DockerHub usernames)
docker pull bilalmagg/aspnet-app:latest
docker pull bilalmagg/fastapi-app:latest
docker pull postgres:15

# Run containers
docker run -d --name postgres -e POSTGRES_PASSWORD=secret -p 5432:5432 postgres:15
docker run -d --name aspnet -p 5000:5000 bilalmagg/aspnet-app:latest
docker run -d --name fastapi -p 8000:8000 bilalmagg/fastapi-app:latest