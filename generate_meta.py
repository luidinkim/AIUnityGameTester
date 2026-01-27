import os
import uuid
import yaml

def generate_guid():
    return uuid.uuid4().hex

def create_meta_content(guid, is_folder=False):
    # 기본 메타 파일 템플릿
    return f"""fileFormatVersion: 2
guid: {guid}
{( "folderAsset: yes" if is_folder else "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: " )}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

def process_directory(root_dir):
    extensions_to_ignore = ['.meta', '.git', '.gitignore', '.DS_Store']
    dirs_to_ignore = ['.git', '__pycache__']

    for root, dirs, files in os.walk(root_dir):
        # 무시할 폴더 제거
        dirs[:] = [d for d in dirs if d not in dirs_to_ignore]

        # 현재 폴더 자체의 메타 파일 생성 (루트는 제외)
        if root != root_dir:
            meta_path = root + ".meta"
            if not os.path.exists(meta_path):
                print(f"Generating meta for folder: {root}")
                with open(meta_path, "w") as f:
                    f.write(create_meta_content(generate_guid(), is_folder=True))

        # 파일 메타 생성
        for file in files:
            if any(file.endswith(ext) for ext in extensions_to_ignore):
                continue
            
            file_path = os.path.join(root, file)
            meta_path = file_path + ".meta"
            
            if not os.path.exists(meta_path):
                print(f"Generating meta for file: {file}")
                # 스크립트(.cs)는 MonoImporter, 나머지는 DefaultImporter를 쓰도록 간단히 처리
                # 위 템플릿은 범용적으로 작성됨.
                with open(meta_path, "w") as f:
                    f.write(create_meta_content(generate_guid(), is_folder=False))

if __name__ == "__main__":
    print("Starting meta file generation...")
    process_directory(".")
    print("Done.")
