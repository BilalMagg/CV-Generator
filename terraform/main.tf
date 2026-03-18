terraform {
  required_providers {
    virtualbox = {
      source  = "terra-farm/virtualbox"
      version = ">=0.3.1"
    }
  }
}

provider "virtualbox" {}

# Create the VM
resource "virtualbox_vm" "app_vm" {
  name   = "DockerHostVM"
  image  = "ubuntu_24_04"  # Name of the Ubuntu box installed in VirtualBox
  cpus   = 2
  memory = 2048

  # Port forwarding for SSH, HTTP, Postgres
  network_adapter {
    type           = "hostonly"
    host_interface = "vboxnet0"  # default VirtualBox host-only adapter
  }

  network_adapter {
    type = "nat"
    nat_network {
      name = "NatNetwork"
    }

    # Forward ports from host to guest
    nat_network_port_forward {
      host_port      = 2222
      guest_port     = 22
      protocol       = "tcp"
      description    = "SSH"
    }

    nat_network_port_forward {
      host_port      = 5000
      guest_port     = 5000
      protocol       = "tcp"
      description    = "ASP.NET"
    }

    nat_network_port_forward {
      host_port      = 8000
      guest_port     = 8000
      protocol       = "tcp"
      description    = "FastAPI"
    }

    nat_network_port_forward {
      host_port      = 5432
      guest_port     = 5432
      protocol       = "tcp"
      description    = "Postgres"
    }
  }

  # Boot script to install Docker and run containers
  provisioner "file" {
    source      = "setup.sh"
    destination = "/home/ubuntu/setup.sh"
  }

  provisioner "remote-exec" {
    inline = [
      "chmod +x /home/ubuntu/setup.sh",
      "sudo /home/ubuntu/setup.sh"
    ]

    connection {
      type        = "ssh"
      user        = "ubuntu"
      private_key = file("~/.ssh/id_rsa")
      host        = "127.0.0.1"
      port        = 2222
    }
  }
}