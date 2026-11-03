#include "CASC.h"

#include <iostream>

namespace casc {
	File::~File() {
		close();
	}

	std::vector<uint8_t> File::read() const {
		const uint32_t size = CascGetFileSize(handle, 0);
		std::vector<uint8_t> buffer(size);

		#ifdef _MSC_VER
		unsigned long bytes_read;
		#else
		unsigned bytes_read;
		#endif
		const bool success = CascReadFile(handle, buffer.data(), size, &bytes_read);
		if (!success) {
			std::cout << "Failed to read file: " << GetLastError() << std::endl;
		}
		return buffer;
	}
	
	size_t File::size() const noexcept {
		return CascGetFileSize(handle, 0);
	}

	void File::close() const noexcept {
		CascCloseFile(handle);
	}

	// CASC
	CASC::CASC(const fs::path& path) {
		open(path);
	}

	CASC::~CASC() {
		close();
	}

	void CASC::open(const fs::path& path) {
		bool opened = CascOpenStorage(path.c_str(), CASC_LOCALE_ALL, &handle);

		// 重制版的 .build.info 在 Data\ 里面，而 1.31 及更早的经典客户端把它放在安装根目录，
		// Data\ 下只有 data/config/indices。这种情况下退到上一级目录再试一次。
		if (!opened) {
			const fs::path parent = path.parent_path();
			if (!parent.empty() && fs::exists(parent / ".build.info")) {
				opened = CascOpenStorage(parent.c_str(), CASC_LOCALE_ALL, &handle);
				if (opened) {
					std::cout << "  .build.info found in " << parent << ", using it as CASC root\n";
				}
			}
		}

		if (!opened) {
			std::wcout << "Error opening " << path << " with error:" << GetLastError() << std::endl;
		}
	}

	File CASC::file_open(const fs::path& path) const {
		File file;
		const bool opened = CascOpenFile(handle, path.string().c_str(), 0, CASC_OPEN_BY_NAME, &file.handle);
		if (!opened) {
			std::cout << "Error opening file " << path << " with error: " << GetLastError() << std::endl;
		}
		return file;
	}

	/// ToDo is there a better way to check if a file exists?
	bool CASC::file_exists(const fs::path& path) const {
		File file;
		return CascOpenFile(handle, path.string().c_str(), 0, CASC_OPEN_BY_NAME, &file.handle);
	}

	void CASC::close() {
		CascCloseStorage(handle);
		handle = nullptr;
	}
}