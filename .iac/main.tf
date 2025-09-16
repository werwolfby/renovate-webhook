terraform {
  required_version = ">= 1.0"
  backend "s3" {
    bucket = "werwolf-terraform"
    key    = "states/renovate-webhook"
    region = "us-west-2"
  }
  required_providers {
    github = {
      source  = "integrations/github"
      version = "~> 6.0"
    }
    docker = {
      source  = "docker/docker"
      version = "~> 0.5"
    }
  }
}
