// Build pipeline (master): the versioned builder zip (dist/Installer-<version>.zip), archived on the build page.
// jenkins/release.Jenkinsfile publishes a chosen build's zip as a GitHub release, by hand. Tests are
// jenkins/test.Jenkinsfile. Setup: docs/ci-jenkins.md.

// Runs a scripts/*.ps1 script through sh. The pwsh step looks pwsh up on the Jenkins process's own PATH, which a
// launchd-started Jenkins lacks (only /usr/bin:/bin:/usr/sbin:/sbin); sh uses the PATH set in environment below.
def runScript(String scriptAndArguments) {
    sh """command -v pwsh > /dev/null || { echo "pwsh not found on PATH (\$PATH). Install it: brew install --cask powershell" >&2; exit 127; }
pwsh -NoLogo -NoProfile -NonInteractive -File ${scriptAndArguments}"""
}

pipeline {
    agent { label 'dotnet10 && macos' }

    options {
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 30, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '30', artifactNumToKeepStr: '10'))
        // The installer-release job copies these artifacts (Copy Artifact plugin).
        copyArtifactPermission('installer-release')
    }

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        DOTNET_CLI_HOME = "${WORKSPACE}/.dotnet-home"
        PATH = "/opt/homebrew/bin:/usr/local/bin:/usr/local/share/dotnet:${HOME}/.dotnet:${PATH}"
    }

    stages {
        stage('Clean') {
            steps { runScript 'scripts/Clean.ps1' }
        }
        stage('Build') {
            steps { runScript 'scripts/Build.ps1 -Configuration Release' }
        }
        stage('Publish') {
            steps { runScript 'scripts/Publish.ps1 -Configuration Release' }
        }
    }

    post {
        success {
            archiveArtifacts artifacts: 'dist/*.zip, dist/*.zip.sha256, dist/version.txt, dist/release.json', fingerprint: true
            script {
                currentBuild.displayName = "#${env.BUILD_NUMBER} ${readFile('dist/version.txt').trim()}"
            }
        }
    }
}
