#!/usr/bin/env bash

# Exit immediately if a command exits with a non-zero status
set -e

# Visual formatting variables
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0;3m' # No Color
INFO="${GREEN}[INFO]${NC}"
ERROR="${RED}[ERROR]${NC}"

echo -e "${INFO} Starting Docker Engine installation..."

# 1. Ensure the script is run as root
if [ "$EUID" -ne 0 ]; then
  echo -e "${ERROR} Please run this script with sudo or as root."
  exit 1
fi

# 2. Uninstall conflicting pre-installed packages
echo -e "${INFO} Removing potential package conflicts..."
for pkg in docker.io docker-doc docker-compose docker-compose-v2 podman-docker containerd runc; do
  apt-get remove -y $pkg >/dev/null 2>&1 || true
done

# 3. Update cache and install core prerequisites
echo -e "${INFO} Updating apt repository index and installing prerequisites..."
apt-get update -y
apt-get install -y ca-certificates curl gnupg

# 4. Set up the official Docker GPG key
echo -e "${INFO} Setting up Docker keyring and GPG key..."
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://docker.com -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

# 5. Deduce architecture and codename, then add official repo
echo -e "${INFO} Adding Docker repository to software sources..."
ARCH=$(dpkg --print-architecture)
CODENAME=$(. /etc/os-release && echo "$VERSION_CODENAME")

echo "deb [arch=${ARCH} signed-by=/etc/apt/keyrings/docker.asc] https://docker.com ${CODENAME} stable" \
  | tee /etc/apt/sources.list.d/docker.list > /dev/null

# 6. Install Docker Engine, CLI, and Compose plugins
echo -e "${INFO} Syncing repository and installing Docker packages..."
apt-get update -y
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# 7. Enable and start the systemd services
echo -e "${INFO} Ensuring Docker service is enabled and active..."
systemctl enable docker
systemctl start docker

# 8. Post-installation check
echo -e "${INFO} Verification: Running hello-world container..."
if docker run --rm hello-world >/dev/null 2>&1; then
  echo -e "${GREEN}[SUCCESS] Docker has been successfully installed and verified!${NC}"
else
  echo -e "${ERROR} Docker installation completed, but the test container failed to run."
  exit 1
fi
