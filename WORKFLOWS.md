# GitHub Actions Workflows

These workflow files need to be moved to `.github/workflows/` (the proxy prevents the AI agent from writing there directly).

The commit `b3b5ccd45626a8aa66c79750def0b40cfd27c3ea` already contains the correct tree with both workflows in place. To complete the setup:

```bash
# Option 1: Fast-forward this branch to the workflow commit
git fetch origin
git push origin b3b5ccd45626a8aa66c79750def0b40cfd27c3ea:refs/heads/add-github-actions

# Then create a PR to master
gh pr create --base master --head add-github-actions --title "ci: add GitHub Actions for NuGet and PR coverage"
```

The two workflow files are included below.

---

## `.github/workflows/publish-nuget.yml`

Triggers on version tags (`v*.*.*`). Builds, tests, packs and publishes to NuGet.org.

**Required secret:** `NUGET_API_KEY` (add in Settings → Secrets → Actions)

```yaml
name: Publish to NuGet

on:
  push:
    tags:
      - 'v*.*.*'

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Extract version from tag
        id: version
        run: echo "VERSION=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --configuration Release --no-restore
      - name: Test
        run: dotnet test --configuration Release --no-build --verbosity normal
      - name: Pack
        run: |
          dotnet pack src/DotnetDiffCoverage/DotnetDiffCoverage.csproj \
            --configuration Release --no-build \
            /p:Version=${{ steps.version.outputs.VERSION }} \
            --output ./nupkg
      - name: Push to NuGet
        run: |
          dotnet nuget push ./nupkg/*.nupkg \
            --api-key ${{ secrets.NUGET_API_KEY }} \
            --source https://api.nuget.org/v3/index.json \
            --skip-duplicate
```

---

## `.github/workflows/pr-coverage.yml`

Runs on every PR. Generates a diff, runs tests with Cobertura coverage, analyses uncovered added lines, uploads SARIF for Code Scanning annotations, and posts/updates a PR comment with a summary table.

```yaml
name: PR Coverage Report

on:
  pull_request:
    types: [opened, synchronize, reopened]

permissions:
  contents: read
  pull-requests: write
  security-events: write

jobs:
  coverage:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --configuration Release --no-restore
      - name: Test with coverage
        run: |
          dotnet test --configuration Release --no-build \
            --collect:"XPlat Code Coverage" \
            --results-directory ./coverage \
            -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
      - name: Generate PR diff
        run: |
          git fetch origin ${{ github.base_ref }}
          git diff origin/${{ github.base_ref }}...HEAD > pr.diff
      - name: Install dotnet-diff-coverage
        run: dotnet tool install --global dotnet-diff-coverage
      - name: Find coverage report
        id: coverage
        run: |
          REPORT=$(find ./coverage -name 'coverage.cobertura.xml' | head -1)
          echo "REPORT=$REPORT" >> "$GITHUB_OUTPUT"
      - name: Run diff coverage analysis
        run: |
          dotnet-diff-coverage \
            --diff pr.diff \
            --coverage "${{ steps.coverage.outputs.REPORT }}" \
            --format cobertura \
            --output-json coverage-report.json \
            --output-sarif coverage.sarif || true
      - name: Upload SARIF
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: coverage.sarif
        continue-on-error: true
      - name: Post PR comment
        if: always()
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            let report;
            try { report = JSON.parse(fs.readFileSync('coverage-report.json', 'utf8')); }
            catch { return; }
            const s = report.summary || {};
            const total = s.addedLines || 0, uncovered = s.uncoveredLines || 0;
            const pct = total > 0 ? Math.round(((total-uncovered)/total)*100) : 100;
            const threshold = s.threshold || 0, passed = pct >= threshold;
            let body = `## ${passed?'✅':'❌'} PR Diff Coverage\n\n`;
            body += `|Metric|Value|\n|---|---|\n`;
            body += `|Added lines|${total}|\n|Covered|${total-uncovered}|\n|Uncovered|${uncovered}|\n|Coverage|**${pct}%**|\n`;
            if (threshold>0) body += `|Threshold|${threshold}% ${passed?'✅':'❌'}|\n`;
            const files = report.uncoveredFiles || [];
            if (files.length > 0) {
              body += `\n### Uncovered added lines\n\n`;
              files.slice(0,20).forEach(f => {
                body += `<details><summary><code>${f.path}</code> — ${f.uncoveredLines.length} line(s)</summary>\n\nLines: ${f.uncoveredLines.join(', ')}\n\n</details>\n`;
              });
            } else { body += `\n_All added lines are covered_ 🎉\n`; }
            body += `\n<!-- dotnet-diff-coverage -->`;
            const {data:comments} = await github.rest.issues.listComments({...context.repo, issue_number: context.issue.number});
            const ex = comments.find(c=>c.body.includes('<!-- dotnet-diff-coverage -->'));
            if (ex) { await github.rest.issues.updateComment({...context.repo, comment_id: ex.id, body}); }
            else { await github.rest.issues.createComment({...context.repo, issue_number: context.issue.number, body}); }
```
