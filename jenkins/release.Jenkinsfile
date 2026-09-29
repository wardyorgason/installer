// Release pipeline (run by hand): publishes a zip that the installer-build job already built and archived as a GitHub
// release. It never rebuilds: it copies that build's artifacts, checks the zip's SHA-256, and creates the release and
// tag on the commit the zip came from (scripts/Publish-Release.ps1, GitHub REST API, no gh CLI). Setup: docs/ci-jenkins.md.

// Runs a scripts/*.ps1 script through sh. The pwsh step looks pwsh up on the Jenkins process's own PATH, which a
// launchd-started Jenkins lacks (only /usr/bin:/bin:/usr/sbin:/sbin); sh uses the PATH set in environment below.
def runScript(String scriptAndArguments) {
    sh """command -v pwsh > /dev/null || { echo "pwsh not found on PATH (\$PATH). Install it: brew install --cask powershell" >&2; exit 127; }
pwsh -NoLogo -NoProfile -NonInteractive -File ${scriptAndArguments}"""
}

pipeline {
    agent { label 'dotnet10 && macos' }

    parameters {
        string(name: 'SOURCE_BUILD', defaultValue: '', trim: true,
            description: 'installer-build run to release. Empty: its last successful build.')
        booleanParam(name: 'PRERELEASE', defaultValue: false,
            description: 'Mark the GitHub release as a pre-release.')
        string(name: 'GITHUB_REPOSITORY', defaultValue: 'wardyorgason/installer', trim: true,
            description: 'owner/name of the GitHub repository to publish to.')
    }

    options {
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 15, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '30'))
    }

    environment {
        PATH = "/opt/homebrew/bin:/usr/local/bin:/usr/local/share/dotnet:${HOME}/.dotnet:${PATH}"
    }

    stages {
        stage('Clean') {
            steps { runScript 'scripts/Clean.ps1' }
        }
        stage('Fetch artifacts') {
            steps {
                script {
                    def selector = params.SOURCE_BUILD ? specific(params.SOURCE_BUILD) : lastSuccessful()
                    copyArtifacts(projectName: 'installer-build', selector: selector, filter: 'dist/*', fingerprintArtifacts: true)
                }
            }
        }
        stage('Publish release') {
            steps {
                withCredentials([string(credentialsId: 'github-installer-release', variable: 'GITHUB_TOKEN')]) {
                    runScript "scripts/Publish-Release.ps1 -Repository '${params.GITHUB_REPOSITORY}'${params.PRERELEASE ? ' -Prerelease' : ''}"
                }
            }
        }
    }

    post {
        success {
            script {
                currentBuild.displayName = "#${env.BUILD_NUMBER} ${readFile('dist/version.txt').trim()}"
                currentBuild.description = readFile('dist/release-url.txt').trim()
            }
        }
    }
}
