terraform {
  required_providers {
    vmworkstation = {
      source  = "elsudano/vmworkstation"
      version = "1.0.4"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
  }
  required_version = ">= 1.0.0"
}

provider "vmworkstation" {
  url      = "http://127.0.0.1:8697/api"
  user     = "user"
  password = "password"
  https    = false
  debug    = false
}

resource "random_id" "vm_suffix" {
  byte_length = 4
}

resource "vmworkstation_vm" "my_vm" {
  sourceid     = "M2CA80KMFMEK3JA0TM6797U26DFHI8F3"
  denomination = "my-terraform-vm-${random_id.vm_suffix.hex}"
  description  = "VM created by Terraform"
  path         = "C:\\Users\\Stronger\\Documents\\Virtual Machines\\my-terraform-vm-${random_id.vm_suffix.hex}\\my-terraform-vm-${random_id.vm_suffix.hex}.vmx"
  processors   = 2
  memory       = 2048
}

output "vm_name" {
  value = vmworkstation_vm.my_vm.denomination
}

output "vm_id" {
  value = vmworkstation_vm.my_vm.id
}