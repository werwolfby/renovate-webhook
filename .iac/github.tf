provider "github" {
  owner = "werwolfby"
}

resource "github_repository" "renovate_webhook" {
  name        = "renovate-webhook"
  description = "Webhook service for Renovate bot"
  visibility  = "private"

  has_issues    = true
  has_projects  = false
  has_wiki      = false
  has_downloads = true

  allow_squash_merge = false
  allow_merge_commit = true
  allow_rebase_merge = false

  delete_branch_on_merge = true
  auto_init              = false
}

resource "github_actions_secret" "dockerhub_username" {
  repository      = github_repository.renovate_webhook.name
  secret_name     = "DOCKERHUB_USERNAME"
  plaintext_value = docker_hub_repository.renovate_webhook.namespace
}

resource "github_actions_secret" "dockerhub_password" {
  repository      = github_repository.renovate_webhook.name
  secret_name     = "DOCKERHUB_PASSWORD"
  plaintext_value = docker_access_token.githug_token.token
}
