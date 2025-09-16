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
