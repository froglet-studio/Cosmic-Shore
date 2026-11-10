"""agenome.cli with the ext ops installed: python3 -m ops_ext.cli render|sheet|score ..."""
import sys

import ops_ext

ops_ext.install()
from agenome.cli import main  # noqa: E402

if __name__ == "__main__":
    main(sys.argv[1:])
