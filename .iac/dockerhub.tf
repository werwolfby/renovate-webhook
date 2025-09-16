terraform {
  required_providers {
    docker = {
      source  = "docker/docker"
      version = "~> 0.5"
    }
  }
}

provider "docker" {
}

resource "docker_hub_repository" "renovate_webhook" {
  namespace   = "werwolfby"
  name        = "renovate-webhook"
  description = "Webhook service for Renovate bot"
}

resource "docker_access_token" "githug_token" {
  token_label = "GitHub ${docker_hub_repository.renovate_webhook.namespace}/${docker_hub_repository.renovate_webhook.name}"
  
  scopes = [
    "repo:read",
    "repo:write", 
    "repo:public_read"
  ]
}
