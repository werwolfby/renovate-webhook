terraform {
  required_version = ">= 1.0"
  backend "s3" {
    bucket = "werwolf-terraform"
    key    = "states/renovate-webhook"
    region = "us-west-2"
  }
}
