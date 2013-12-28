# Because localStorage isn't available on file:// urls (wtf??)

store = window.localStorage
if store?
	datum = Date.now() + "a"
	store.setItem 'localStorage test', datum
	store = null unless datum == store.getItem 'localStorage test'
	store?.removeItem 'localStorage test'

unless store?
	s = {}
	store =
		getItem: (item) -> s[item]
		setItem: (item, value) -> s[item] = value
		deleteItem: (item) -> delete s[item]

window.rohStore = store
