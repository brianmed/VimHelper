function! VimHelper(findstart, base)
    if a:findstart
        " locate the start of the .
        let line = getline('.')
        let atTheDotIdx = col('.')

        while 0 < atTheDotIdx && line[atTheDotIdx] !~ '\.'
            let atTheDotIdx -= 1
        endwhile

        let beforeDotPos = getpos('.')
        let beforeDotPos[3] = atTheDotIdx - 1

        call setpos('.', beforeDotPos)

        return atTheDotIdx + 1
    endif

	let channel = ch_open('127.0.0.1:5555', {"mode": "nl"})

    if "open" != ch_status(channel)
        echo "Connection failed: 127.0.0.1:5555"

        return
    endif

    let ret = []

    let absPath = expand('%:p')
    let offset = line2byte('.') + col('.') - 3
    let lookup = [absPath,offset]

	call ch_sendraw(channel, json_encode(lookup))
	call ch_sendraw(channel, "\n")
	let infoTainment = json_decode(ch_read(channel, {'timeout': 20000}))

    if "open" != ch_status(channel)
        echo "Connection failed: 127.0.0.1:5555"

        return
    endif

    call ch_close(channel)

    for group in keys(infoTainment)
        for field in infoTainment[group]
            call add(ret, field.Name)
        endfor
    endfor

    return ret
endfunction

setlocal omnifunc=VimHelper
