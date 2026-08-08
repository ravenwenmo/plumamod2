import os
from PIL import Image

def create_blank_pngs(src_dir, dst_dir, size=(250, 190)):
    """
    从 src_dir 中读取所有 .cs 文件，在 dst_dir 中生成同名的空白 PNG 图片。
    若目标 PNG 已存在，则跳过该文件。

    :param src_dir: 源文件夹路径（包含 .cs 文件）
    :param dst_dir: 目标文件夹路径（生成的 PNG 将保存于此）
    :param size: 图片尺寸，默认为 (250, 190)
    """
    os.makedirs(dst_dir, exist_ok=True)

    for filename in os.listdir(src_dir):
        if filename.endswith('.cs'):
            base_name = os.path.splitext(filename)[0]
            png_name = base_name + '.png'
            png_path = os.path.join(dst_dir, png_name)

            # 检查文件是否已存在
            if os.path.exists(png_path):
                print(f'跳过已存在: {png_path}')
                continue

            img = Image.new('RGB', size, color='white')
            img.save(png_path)
            print(f'生成: {png_path}')

if __name__ == '__main__':
    import sys

    if len(sys.argv) >= 3:
        src_folder = sys.argv[1]
        dst_folder = sys.argv[2]
    else:
        src_folder = input("请输入源文件夹路径: ").strip()
        dst_folder = input("请输入目标文件夹路径: ").strip()

    if not os.path.isdir(src_folder):
        print(f"错误：源文件夹 '{src_folder}' 不存在。")
        sys.exit(1)

    create_blank_pngs(src_folder, dst_folder)