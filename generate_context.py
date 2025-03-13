import os
import datetime

# Files and directories to ignore
IGNORE_PATTERNS = [
    '.env',
    '.git',
    '.idea',
    '.cursor',
    '__pycache__',
    '.vs',
    '.vscode',
    'node_modules',
    '.DS_Store',
    '*.pyc',
    '*.pyo',
    '*.pyd',
    '*.so',
    '*.dll',
    '*.dylib',
    '*.log',
    '*.pot',
    '*.pyc',
    '*.pyo',
    '*.sln',
    '*.user',
    '*.suo',
    '*.cache',
    'bin',
    'obj',
    'dist',
    'build'
]

def should_ignore(path):
    """Check if a path should be ignored based on patterns."""
    path_parts = path.split(os.sep)
    for pattern in IGNORE_PATTERNS:
        if pattern.startswith('*'):
            if any(part.endswith(pattern[1:]) for part in path_parts):
                return True
        elif pattern in path_parts:
            return True
    return False

def is_binary(file_path):
    """Check if a file is binary."""
    try:
        with open(file_path, 'tr') as check_file:
            check_file.read(1024)
            return False
    except UnicodeDecodeError:
        return True

def generate_context_document():
    """Generate a context document from all relevant files in the repository."""
    output_filename = f'context_{datetime.datetime.now().strftime("%Y%m%d_%H%M%S")}.txt'
    
    with open(output_filename, 'w', encoding='utf-8') as output_file:
        # Write header
        output_file.write(f"Repository Context Document\n")
        output_file.write(f"Generated on: {datetime.datetime.now()}\n")
        output_file.write("=" * 80 + "\n\n")

        # Walk through repository
        for root, dirs, files in os.walk('.'):
            # Skip ignored directories
            dirs[:] = [d for d in dirs if not should_ignore(os.path.join(root, d))]
            
            for file in files:
                file_path = os.path.join(root, file)
                
                # Skip ignored files
                if should_ignore(file_path):
                    continue
                
                # Skip binary files
                if is_binary(file_path):
                    continue
                
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        content = f.read()
                        
                        # Write file header
                        output_file.write(f"File: {file_path}\n")
                        output_file.write("-" * 80 + "\n")
                        
                        # Write file content
                        output_file.write(content)
                        output_file.write("\n\n")
                        output_file.write("=" * 80 + "\n\n")
                except Exception as e:
                    print(f"Error processing {file_path}: {str(e)}")

    print(f"Context document generated: {output_filename}")

if __name__ == "__main__":
    generate_context_document() 