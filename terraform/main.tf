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
  url      = "http://localhost:8697/api"
  user     = "hamid"
  password = "Hamid@123"
  https    = false
  debug    = false
}

resource "random_id" "vm_suffix" {
  byte_length = 4
}

resource "vmworkstation_vm" "my_vm" {
  sourceid     = "KOEGDG921ESMRFMLAIQ858LBPGR3K8GK"
  denomination = "my-terraform-vm-${random_id.vm_suffix.hex}"
  description  = "VM created by Terraform"
  path         = "C:\\Users\\mohsi\\Documents\\Virtual Machines\\my-terraform-vm-${random_id.vm_suffix.hex}\\my-terraform-vm-${random_id.vm_suffix.hex}.vmx"
  processors   = 2
  memory       = 2048
}

output "vm_name" {
  value = vmworkstation_vm.my_vm.denomination
}

output "vm_id" {
  value = vmworkstation_vm.my_vm.id
}