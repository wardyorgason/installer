// Release pipeline (run by hand): publishes a zip that the installer-build job already built and archived as a GitHub
// release. It never rebuilds: it copies that build's artifacts, checks the zip's SHA-256, and creates the release and
// tag on the commit the zip came from (scripts/Publish-Release.ps1, GitHub REST API, no gh CLI). Setup: docs/ci-jenkins.md.
pipeline {
    agent { label 'dotnet10 && macos' }

    parameters {
        string(name: 'SOURCE_BRANCH', defaultValue: 'master', trim: true,
            description: 'Branch of the installer-build job whose artifacts to release.')
        string(name: 'SOURCE_BUILD', defaultValue: '', trim: true,
            description: 'Build number of that branch to release. Empty: its last successful build.')
        booleanParam(name: 'PRERELEASE', defaultValue: false,
            description: 'Mark the GitHub release as a pre-release (for example when releasing a feature branch).')
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
            steps { pwsh 'scripts/Clean.ps1' }
        }
        stage('Fetch artifacts') {
            steps {
                script {
                    // Multibranch jobs are named <job>/<branch>, with '/' in branch names encoded.
                    def project = "installer-build/${params.SOURCE_BRANCH.replace('/', '%2F')}"
                    def selector = params.SOURCE_BUILD ? specific(params.SOURCE_BUILD) : lastSuccessful()
                    copyArtifacts(projectName: project, selector: selector, filter: 'dist/*', fingerprintArtifacts: true)
                }
            }
        }
        stage('Publish release') {
            steps {
                withCredentials([string(credentialsId: 'github-installer-release', variable: 'GITHUB_TOKEN')]) {
                    pwsh "scripts/Publish-Release.ps1 -Repository '${params.GITHUB_REPOSITORY}'${params.PRERELEASE ? ' -Prerelease' : ''}"
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
