// Test pipeline: every UnitTests.* project on the Mac agent, including the Integration tests (real codesign, makensis,
// Docker) and the EndToEnd tests (the whole app packaging the samples for every platform). Results are JUnit on the
// build page. Setup: docs/ci-jenkins.md.

// Runs a scripts/*.ps1 script through sh. The pwsh step looks pwsh up on the Jenkins process's own PATH, which a
// launchd-started Jenkins lacks (only /usr/bin:/bin:/usr/sbin:/sbin); sh uses the PATH set in environment below.
def runScript(String scriptAndArguments) {
    sh """command -v pwsh > /dev/null || { echo "pwsh not found on PATH (\$PATH). Install it: brew install --cask powershell" >&2; exit 127; }
pwsh -NoLogo -NoProfile -NonInteractive -File ${scriptAndArguments}"""
}

pipeline {
    agent { label 'dotnet10 && macos' }

    parameters {
        booleanParam(name: 'UNLOCK_KEYCHAIN', defaultValue: false,
            description: 'Unlock the login keychain before signing. Only needed when Jenkins runs as a launchd daemon without a logged-in session; reads the mac-login-keychain secret text credential.')
        booleanParam(name: 'NOTARIZE', defaultValue: false,
            description: 'Also run the notarization integration test. Needs INSTALLER_TEST_DEVELOPER_ID and INSTALLER_TEST_NOTARY_PROFILE on the agent.')
    }

    options {
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 60, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '50'))
    }

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        DOTNET_CLI_HOME = "${WORKSPACE}/.dotnet-home"
        // Homebrew and dotnet installer locations that a launchd-started Jenkins does not have on PATH.
        PATH = "/opt/homebrew/bin:/usr/local/bin:/usr/local/share/dotnet:${HOME}/.dotnet:${PATH}"
        INSTALLER_TEST_NOTARIZE = "${params.NOTARIZE ? '1' : ''}"
    }

    stages {
        stage('Clean') {
            steps { runScript 'scripts/Clean.ps1' }
        }
        stage('Unlock keychain') {
            when { expression { params.UNLOCK_KEYCHAIN } }
            steps {
                withCredentials([string(credentialsId: 'mac-login-keychain', variable: 'KEYCHAIN_PASSWORD')]) {
                    sh 'security unlock-keychain -p "$KEYCHAIN_PASSWORD" "$HOME/Library/Keychains/login.keychain-db"'
                }
            }
        }
        stage('Tests') {
            steps { runScript 'scripts/Test.ps1 -Configuration Release -IncludeIntegration -IncludeEndToEnd' }
        }
    }

    post {
        always {
            junit testResults: 'dist/test-results/*.xml', allowEmptyResults: false, keepLongStdio: true
            archiveArtifacts artifacts: 'dist/test-results/**', allowEmptyArchive: true
        }
    }
}
