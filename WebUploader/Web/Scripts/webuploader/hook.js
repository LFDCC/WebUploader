/**
 * 安装钩子函数
 * @param {any} options
 */
WebUploader.InstallHook = function (options) {

    WebUploader.Uploader.register({
        "add-file": "addFile",
        "before-send-file": "beforeSendFile",
        "before-send": "beforeSend",
        "after-send-file": "afterSendFile"
    }, {
            beforeSendFile: function (file) {//每个选中的文件上传前调用
                return options.beforeSendFile && options.beforeSendFile(file);
            },
            beforeSend: function (block) {//每个chunk文件上传前调用
                return options.beforeSend && options.beforeSend(block);

            },
            afterSendFile: function (file) {
                return options.afterSendFile && options.afterSendFile(file);
            },
            addFile: function (file) {
                return options.addFile && options.addFile(file);
            }
        });
}