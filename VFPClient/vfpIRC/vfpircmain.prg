set memowidth to 8000

clear
set classlib to vfpirc

set default to (sys(5)+sys(2003))

appPath=sys(5)+sys(2003)+"\"

public myIRC, myID
myID=sys(3)

local i
i=adir(IRCList,appPath+"IRC*.xml")

do case
	case i=0
		wait window "No IRC*.xml files available"
		myIRC=""
		
	case i=1
		myIRC=IRCList[1,1]
		
	otherwise
		myIRC=getfile("xml")
endcase

release IRCList


oFrmStatus=createobject("frmStatus")
oFrmStatus.visible=.T.


read events