using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.XCodeEditor;
#endif
using System.IO;

public static class XCodePostProcess
{

#if UNITY_EDITOR
	[PostProcessBuild(999)]
	public static void OnPostProcessBuild( BuildTarget target, string pathToBuiltProject )
	{
		if (target != BuildTarget.iPhone) {
			Debug.LogWarning("Target is not iPhone. XCodePostProcess will not run");
			return;
		}

        //�õ�xcode���̵�·��
        string path = Path.GetFullPath (pathToBuiltProject);

        // Create a new project object from build target
        XCProject project = new XCProject (pathToBuiltProject);

        // Find and run through all projmods files to patch the project.
        // Please pay attention that ALL projmods files in your project folder will be excuted!
        //���������frameworks��������xcode��������
        string[] files = Directory.GetFiles (Application.dataPath, "*.projmods", SearchOption.AllDirectories);
        foreach (string file in files) {
            project.ApplyMod (file);
        }

        //����һ�������ǡ���û�еĻ�sharesdk�ᱨ����
        project.AddOtherLinkerFlags("-licucore");

        //����ǩ����֤�飬 �ڶ������� ��������ó����֤��
		project.overwriteBuildSetting ("CODE_SIGN_IDENTITY", "xxxxxx", "Release");
        project.overwriteBuildSetting ("CODE_SIGN_IDENTITY", "xxxxxx", "Debug");


        // �༭plist �ļ�
        EditorPlist(path);

        //�༭�����ļ�
        EditorCode(path);

        // Finally save the xcode project
        project.Save ();




    }

    private static void EditorPlist(string filePath)
    {
     
        XCPlist list =new XCPlist(filePath);
        string bundle = "com.yusong.momo";

        string PlistAdd = @"  
            <key>CFBundleURLTypes</key>
            <array>
            <dict>
            <key>CFBundleTypeRole</key>
            <string>Editor</string>
            <key>CFBundleURLIconFile</key>
            <string>Icon@2x</string>
            <key>CFBundleURLName</key>
            <string>"+bundle+@"</string>
            <key>CFBundleURLSchemes</key>
            <array>
            <string>ww123456</string>
            </array>
            </dict>
            </array>";
        
        //��plist��������һ��
        list.AddKey(PlistAdd);
        //��plist�����滻һ��
        list.ReplaceKey("<string>com.yusong.${PRODUCT_NAME}</string>","<string>"+bundle+"</string>");
        //����
        list.Save();

    }

    private static void EditorCode(string filePath)
    {
		//��ȡUnityAppController.mm�ļ�
        XClass UnityAppController = new XClass(filePath + "/Classes/UnityAppController.mm");

        //��ָ�������������һ�д���
        UnityAppController.WriteBelow("#include \"PluginBase/AppDelegateListener.h\"","#import <ShareSDK/ShareSDK.h>");

        //��ָ���������滻һ��
        UnityAppController.Replace("return YES;","return [ShareSDK handleOpenURL:url sourceApplication:sourceApplication annotation:annotation wxDelegate:nil];");

        //��ָ�������������һ��
        UnityAppController.WriteBelow("UnityCleanup();\n}","- (BOOL)application:(UIApplication *)application handleOpenURL:(NSURL *)url\r{\r    return [ShareSDK handleOpenURL:url wxDelegate:nil];\r}");


    }

    #endif
}
