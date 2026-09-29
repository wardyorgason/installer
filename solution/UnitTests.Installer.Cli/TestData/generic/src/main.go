// The non-.NET end-to-end sample: proves the builder packages a payload with no .NET files.
// Rebuild the committed binaries with ../build.sh after changing this file.
package main

import (
	"fmt"
	"os"
	"strings"
)

func main() {
	fmt.Println("generic-sample 1.0.0")
	fmt.Println("args: " + strings.Join(os.Args[1:], " "))
}
