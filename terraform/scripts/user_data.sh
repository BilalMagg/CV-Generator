#!/bin/bash
set -e

echo "=== Starting VM bootstrap ==="

# Update system
apt-get update
apt-get upgrade -y

# Install Docker
echo "Installing Docker..."
curl -fsSL https://get.docker.com -o /tmp/get-docker.sh
sh /tmp/get-docker.sh
usermod -aG docker vagrant

# Install Docker Compose plugin
apt-get install -y docker-compose-plugin

# Start Docker
systemctl enable docker
systemctl start docker

# Wait for Docker to be ready
sleep 5

# Clone your repository (replace with your actual repo)
echo "Cloning repository..."
cd /home/vagrant
if [ ! -d "CV-Generator" ]; then
    git https://github.com/BilalMagg/CV-Generator.git
else
    cd CV-Generator && git pull
fi

cd CV-Generator

# Get the host-only IP for service URLs
HOSTONLY_IP=$(ip addr show | grep -oP '(?<=inet\s)192.168.\d+.\d+' | grep -v 192.168.56.1 | head -1)

# Create .env file if needed
cat > .env << EOF
# Use host-only IP for external access
BACKEND_URL=http://${HOSTONLY_IP}:5000
FRONTEND_URL=http://${HOSTONLY_IP}:4200
KEYCLOAK_URL=http://${HOSTONLY_IP}:8080
DATABASE_URL=postgresql://postgres:postgres@cv-postgres:5432/cvdb
EOF

# Start containers
echo "Starting Docker containers..."
docker compose up -d

echo ""
echo "=== Bootstrap complete! ==="
echo "VM IP on host-only network: ${HOSTONLY_IP}"
echo "SSH: ssh vagrant@${HOSTONLY_IP}"
echo ""
echo "Services available at:"
echo "  Frontend:  http://${HOSTONLY_IP}:4200"
echo "  Backend:   http://${HOSTONLY_IP}:5000"
echo "  AI Agents: http://${HOSTONLY_IP}:8000"
echo "  Keycloak:  http://${HOSTONLY_IP}:8080"
echo "  PostgreSQL: ${HOSTONLY_IP}:5432"