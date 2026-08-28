# 项目说明
本项目是一套 Unity 2D 练手学习示例，仅供技术学习、参考交流使用。
> ⚠️本仓库**不包含完整美术资源与配置数据**，克隆项目后部分功能无法直接运行，需要自行补充缺失资源文件。

## 📦缺失资源说明
项目剔除了第三方资源与自定义配置文件，如需运行程序成功，请自行补全下面目录与文件：

### 资源目录
```
Assets/Resources/booth
Assets/Resources/ui/inventoryicons
```

### 配置文件
```
Assets/Core/Configs/_beards.json
Assets/Core/Configs/_boothbg.json
Assets/Core/Configs/_characters.json
Assets/Core/Configs/_clothes.json
Assets/Core/Configs/_emotes.json
Assets/Core/Configs/_glasses.json
Assets/Core/Configs/_hats.json
Assets/Core/Configs/_item.json
Assets/Core/Configs/_languages.json
Assets/Core/Configs/_neck.json
Assets/Core/Configs/_pets.json
Assets/Core/Configs/_umbrellas.json
Assets/Core/Configs/_weapons.json
```

### 第三方付费插件
```
Assets/Le Tai's Asset/Translucent Image （必须URP支持，v5.5.0）
Assets/Modern UI Pack （v5.5.25）
```

## 🚀快速开始
1. 将仓库克隆至本地
2. 使用 Unity 打开项目
3. 根据上面清单，补全缺失资源、配置文件（推荐自己写一个Py脚本遍历一遍资源剔除掉无用资源）
4. 将配置文件对照名字 拖入到0号场景中的循环图标组件中的 DeserializationCharacterData 组件
5. 完成后即可正常运行项目

## 📄许可证
详情参考 LICENSE 文件，注意本项目的MIT许可证非原版，具体添加新许可证：“Commons Clause” License Condition v1.0

---

如果你想要更极简版本，我也可以再压缩一版。