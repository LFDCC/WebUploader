# WebUploader
webuploader分片上传续传秒传.NET


## 续传
![续传](img/continue.gif)

## 秒传
![秒传](img/finished.gif)

##前端实现
1、在webuploader钩子函数beforeSendFile中获取文件的md5值，并验证文件
```javascript
 beforeSendFile: function (file) {
    var deferred = $.Deferred()
        , fileObj = $('#' + file.id);

    uploader.md5File(file, 0, 5 * 1024 * 1024)    
        //获取文件区间的md5值（0-5M）不填区间值获取整个文件的md5值
        //IE9及以下会用flash把整个文件流都读到内存中，所以flash下设置区间是无效的，都很慢

        // 及时显示进度
        // jq3.x版本不进progress方法
        .progress(function (percentage) {
            fileObj.find('.progress span').width(percentage * 100 + "%")
            fileObj.find('.progress label').text("正在验证文件：" + parseInt(percentage * 100) + "%");
        })
        // 如果读取出错了，则通过reject告诉webuploader文件上传出错。
        .fail(function () {
            deferred.reject();
        })
        // 完成
        .then(function (val) {
            $.ajax({
                url: '/Uploader/CheckFullFile',
                data: { md5: val, ext: file.ext, size: file.size },
                dataType: 'json',
                success: function (res) {

                    uploader.options.formData.md5 = val;
                    file.md5 = val;//当前文件的md5值
                    file.uploadedChunkNums = res.chunkNums;//当前文件已经上传的分片数

                    fileObj.find('.progress span').width("0")
                    fileObj.find('.progress label').text("");
                    if (res.ifExist) {
                        fileObj.find('.progress span').animate({ width: '100%' });
                        fileObj.find('.progress label').text("上传成功！");
                        uploader.skipFile(file);
                    }
                    deferred.resolve();
                },
                error: function () {
                    deferred.resolve();
                }
            });
        });
    return deferred.promise();
}
```
2、在webuploader钩子函数beforeSend中处理需要上传的分片文件
```javascript
beforeSend: function (block) {//每个chunk文件上传前调用
    var file = block.file,
        deferred = $.Deferred();

    //如果不能保证已经上传的分片文件被人为破坏,就需要每次上传分片都检测一下分片的完整性
    if (block.chunk < file.uploadedChunkNums) {
        deferred.reject();
    } else {
        deferred.resolve();//只上传大于服务端序号的文件
    }
    return deferred.promise();

}
```
3、上传成功，合并文件
```javascript
 uploader.on('uploadSuccess', function (file, res) {
    uploader.removeFile(file, true);
    var fileObj = $('#' + file.id);
    fileObj.find('.progress label').text("上传成功！");
    fileObj.fadeOut(2000, function () {
        fileObj.remove();
    });
    if (res && res.isUpload) {
        $.post('/Uploader/MergeFiles', { md5: file.md5, ext: file.ext },
            function (data) {
                if (data.isMerge) {
                    console.info('合并成功！');
                } else {
                    console.info('合并失败！');
                }
            });
    }
});
```

##后端实现
1、检测文件是否已经上传，如果没上传则获取已经上传的分片数量
```csharp
public ActionResult CheckFullFile()
{
    try
    {
        var md5 = Convert.ToString(Request["md5"]);
        var ext = Convert.ToString(Request["ext"]);
        var size = Convert.ToInt64(Request["size"]);

        var fileName = md5 + "." + ext;
        var filePath = Server.MapPath(FilesDir) + "/" + fileName;//当前文件目录

        FileInfo file = new FileInfo(filePath);
        if (!file.Exists)
        {
            var chunkNums = 0;
            var chunkPath = Server.MapPath(ChunksDir + "/" + md5);//当前分片文件目录
            if (Directory.Exists(chunkPath))
            {
                DirectoryInfo dicInfo = new DirectoryInfo(chunkPath);
                var files = dicInfo.GetFiles();
                chunkNums = files.Count();
                if (chunkNums > 1)
                {
                    chunkNums = chunkNums - 1; //当文件上传中时，页面刷新，上传中断，这时最后一个保存的块的大小可能会有异常，所以这里直接删除最后一个块文件
                }
            }
            return Json(new { ifExist = file.Exists, chunkNums }, JsonRequestBehavior.AllowGet);
        }
        else
        {
            return Json(new { ifExist = file.Exists }, JsonRequestBehavior.AllowGet);
        }


    }
    catch (Exception e)
    {
        return Json(new { ifExist = false }, JsonRequestBehavior.AllowGet);
    }
} 
```
2、上传分片文件
```csharp
public ActionResult Upload(HttpPostedFileBase file)
{
    if (file != null)
    {
        string md5 = Request["md5"];
        //如果进行了分片
        if (Request.Form.AllKeys.Any(m => m == "chunk"))
        {
            int chunk = Convert.ToInt32(Request.Form["chunk"]);//当前分片在上传分片中的顺序（从0开始）
            string chunkDir = Server.MapPath(ChunksDir + "/" + md5);//当前分片文件目录
            string chunkPath = chunkDir + "/" + chunk;//分片文件路径

            //建立临时传输文件夹
            if (!Directory.Exists(chunkDir))
            {
                Directory.CreateDirectory(chunkDir);
            }
            file.SaveAs(chunkPath);
            return Json(new { isUpload = true, ext = Path.GetExtension(file.FileName) }, JsonRequestBehavior.AllowGet);
        }
        else//没有分片直接保存
        {
            var fileDir = Server.MapPath(FilesDir) + "/" + md5;
            string filePath = fileDir + Path.GetExtension(Request.Files[0].FileName);
            file.SaveAs(filePath);
            return Json(new { isUpload = true, savePath = filePath }, JsonRequestBehavior.AllowGet);
        }
    }
    else
    {
        return Json(new { isUpload = false }, JsonRequestBehavior.AllowGet);
    }
}
```
3、合并文件 并删除分片文件
```csharp
public ActionResult MergeFiles()
{
    string md5 = Request["md5"];
    string ext = Request["ext"];
    var fileName = md5 + "." + ext;
    string sourcePath = Server.MapPath(ChunksDir + "/" + md5);//源数据文件夹
    string targetFilePath = Server.MapPath(FilesDir) + "/" + fileName; //合并后的文件

    if (!Directory.Exists(Server.MapPath(FilesDir)))
    {
        Directory.CreateDirectory(Server.MapPath(FilesDir));
    }
    if (Directory.Exists(sourcePath))
    {
        DirectoryInfo dicInfo = new DirectoryInfo(sourcePath);
        FileInfo[] files = dicInfo.GetFiles();
        foreach (FileInfo file in files.OrderBy(f => int.Parse(f.Name)))
        {
            FileStream addFile = new FileStream(targetFilePath, FileMode.Append, FileAccess.Write);
            BinaryWriter AddWriter = new BinaryWriter(addFile);

            //获得上传的分片数据流 
            Stream stream = file.Open(FileMode.Open);
            BinaryReader TempReader = new BinaryReader(stream);
            //将上传的分片追加到临时文件末尾
            AddWriter.Write(TempReader.ReadBytes((int)stream.Length));
            //关闭BinaryReader文件阅读器
            TempReader.Close();
            stream.Close();
            AddWriter.Close();
            addFile.Close();

            TempReader.Dispose();
            stream.Dispose();
            AddWriter.Dispose();
            addFile.Dispose();
        }
        DeleteFolder(sourcePath);
        return Json(new { isMerge = true, savePath = HttpUtility.UrlEncode(targetFilePath) }, JsonRequestBehavior.AllowGet);
    }
    else
    {
        return Json(new { isMerge = false, savePath = HttpUtility.UrlEncode(targetFilePath) }, JsonRequestBehavior.AllowGet);
    }
}
private void DeleteFolder(string strPath)
{
    //删除这个目录下的所有子目录
    if (Directory.GetDirectories(strPath).Length > 0)
    {
        foreach (string fl in Directory.GetDirectories(strPath))
        {
            Directory.Delete(fl, true);
        }
    }
    //删除这个目录下的所有文件
    if (Directory.GetFiles(strPath).Length > 0)
    {
        foreach (string f in Directory.GetFiles(strPath))
        {
            System.IO.File.Delete(f);
        }
    }
    Directory.Delete(strPath, true);
}
```