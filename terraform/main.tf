terraform {
  required_providers {
    virtualbox = {
      source  = "terra-farm/virtualbox"
      version = "0.2.2-alpha.1"
    }
  }
}

resource "virtualbox_vm" "terra_server" {
  name   = "terra-server"
  # Use a working Ubuntu 22.04 box
  image  = "https://vagrantcloud.com/generic/boxes/ubuntu2204/versions/4.3.12/providers/virtualbox.box"
  cpus   = 2
  memory = "2048 mib"
  user_data = file("${path.module}/scripts/user_data.sh")

  # Adapter 1: NAT (for internet access)
  network_adapter {
    type = "nat"
  }

  # Adapter 2: Host-Only (for direct access)
  network_adapter {
    type           = "hostonly"
    host_interface = "VirtualBox Host-Only Ethernet Adapter"
  }
}

output "vm_ip_hostonly" {
  value = virtualbox_vm.terra_server.network_adapter[1].ipv4_address
  description = "VM IP on host-only network"
}

output "ssh_command" {
  value = "ssh vagrant@${virtualbox_vm.terra_server.network_adapter[1].ipv4_address}"
}

output "service_urls" {
  value = {
    ssh       = "ssh vagrant@${virtualbox_vm.terra_server.network_adapter[1].ipv4_address}"
    frontend  = "http://${virtualbox_vm.terra_server.network_adapter[1].ipv4_address}:4200"
    backend   = "http://${virtualbox_vm.terra_server.network_adapter[1].ipv4_address}:5000"
    ai_agents = "http://${virtualbox_vm.terra_server.network_adapter[1].ipv4_address}:8000"
    keycloak  = "http://${virtualbox_vm.terra_server.network_adapter[1].ipv4_address}:8080"
    postgres  = "${virtualbox_vm.terra_server.network_adapter[1].ipv4_address}:5432"
  }
}