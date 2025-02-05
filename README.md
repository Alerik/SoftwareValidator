# SoftwareValidator
A quick way to validate and correct errors in a file directory with respect to a master.

## Features
- Display and correct errors in a directory
- Fast parallelized indexing
- Cached indexes
- Command Line

## Usage
Simply use:
```
SoftwareValidator.exe /path/to/master /path/to/current
```
To visualize differences between your current and master directories. Don't worry, nothing happens unless you use `-f` to 'force' corrupted files to be overwritten with master files:
```
SoftwareValidator.exe /path/to/master /path/to/current -f
```

See more flags with `--help`.
